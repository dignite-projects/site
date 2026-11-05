using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.FlexFields.Site.Seo;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Admin.Seo;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Dignite.Site.Mcp.Contents;
using Dignite.Site.Mcp.Seo;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace Dignite.Site.Mcp;

/// <summary>The SEO side of the MCP tools: the default share image, and the width warnings of a stored content.</summary>
public class SiteSeoTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteSeoSettingsTools _seoTools;
    private readonly ContentTools _contentTools;

    public SiteSeoTools_Tests()
    {
        _seoTools = GetRequiredService<SiteSeoSettingsTools>();
        _contentTools = GetRequiredService<ContentTools>();
    }

    [Fact]
    public async Task Should_Set_And_Clear_The_Default_Share_Image()
    {
        var stored = await _seoTools.SetDefaultShareImageAsync(" /api/site-public/files/images/share.jpg ");

        stored.DefaultShareImage.ShouldBe("/api/site-public/files/images/share.jpg");
        (await GetRequiredService<ISiteSeoSettingsAdminAppService>().GetAsync()).DefaultShareImage
            .ShouldBe("/api/site-public/files/images/share.jpg");

        (await _seoTools.SetDefaultShareImageAsync("")).DefaultShareImage.ShouldBeNull();
        (await GetRequiredService<ISiteSeoSettingsAdminAppService>().GetAsync()).DefaultShareImage.ShouldBeNull();
    }

    [Fact]
    public void Setting_The_Default_Share_Image_Should_Need_The_Update_Permission()
    {
        typeof(SiteSeoSettingsTools)
            .GetMethod(nameof(SiteSeoSettingsTools.SetDefaultShareImageAsync))!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ShouldBe(new[] { SiteAdminPermissions.Seo.Update });
    }

    /// <summary>A title too wide for a search result is stored, and the client is told: 40 kanji are 80 columns.</summary>
    [Fact]
    public async Task A_Stored_Content_Should_Warn_Of_An_SEO_Title_Too_Wide()
    {
        var contentType = await CreateSeoContentTypeAsync();

        var created = await _contentTools.CreateContentAsync(
            page: "blog",
            contentType: contentType,
            cultureName: SiteTestData.EnglishCulture,
            slug: $"wide-{Guid.NewGuid():N}",
            fieldValues: new Dictionary<string, object?>
            {
                ["title"] = "Wide",
                // As an MCP client sends it: JSON.
                [SeoFieldNames.FieldName] = JsonSerializer.SerializeToElement(new
                {
                    metaTitle = new string('日', 40),
                    metaDescription = "Short enough."
                })
            },
            status: ContentStatus.Published,
            publishTime: SiteTestData.PublishTime);

        created.Warnings.ShouldNotBeNull();
        created.Warnings.Length.ShouldBe(1);
        created.Warnings[0].ShouldContain("80 columns");
    }

    [Fact]
    public async Task A_Stored_Content_Within_The_Limits_Should_Have_No_Warning()
    {
        var contentType = await CreateSeoContentTypeAsync();

        var created = await _contentTools.CreateContentAsync(
            page: "blog",
            contentType: contentType,
            cultureName: SiteTestData.EnglishCulture,
            slug: $"narrow-{Guid.NewGuid():N}",
            fieldValues: new Dictionary<string, object?>
            {
                ["title"] = "Narrow",
                [SeoFieldNames.FieldName] = JsonSerializer.SerializeToElement(new { metaTitle = new string('日', 30) })
            },
            status: ContentStatus.Published,
            publishTime: SiteTestData.PublishTime);

        created.Warnings.ShouldBeNull();
    }

    /// <summary>Site.Domain.Shared names the SEO field and its keys without referencing the field type's package.</summary>
    [Fact]
    public void The_Names_Should_Be_The_Field_Types()
    {
        SeoTextWidth.SeoFieldName.ShouldBe(SeoFieldNames.FieldName);
        SeoTextWidth.MetaTitleLimitKey.ShouldBe(SeoConfigurationNames.MetaTitleCharLimit);
        SeoTextWidth.MetaDescriptionLimitKey.ShouldBe(SeoConfigurationNames.MetaDescriptionCharLimit);
        SeoTextWidth.DefaultMetaTitleLimit.ShouldBe(new SeoConfiguration().MetaTitleCharLimit);
        SeoTextWidth.DefaultMetaDescriptionLimit.ShouldBe(new SeoConfiguration().MetaDescriptionCharLimit);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("Acme", 4)]
    [InlineData("日本語", 6)]
    [InlineData("한국어", 6)]
    [InlineData("ＡＢ", 4)]
    [InlineData("Dignite 网站", 12)]
    public void Columns_Should_Count_A_Wide_Character_As_Two(string? text, int columns)
    {
        SeoTextWidth.Columns(text).ShouldBe(columns);
    }

    private async Task<string> CreateSeoContentTypeAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            var fieldRepository = GetRequiredService<IFieldRepository>();
            var seoField = await fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);
            var titleField = await fieldRepository.GetAsync(SiteTestData.TitleFieldId);

            var contentType = await GetRequiredService<ContentTypeManager>().CreateAsync(
                SiteTestData.BlogPageId, $"seo-width-{Guid.NewGuid():N}"[..20], "SEO width test",
                fields: new[]
                {
                    new ContentTypeField(titleField.Id, required: true, order: 0),
                    new ContentTypeField(seoField!.Id, order: 1)
                });

            return contentType.Name;
        });
    }
}
