using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Admin.ContentTypes;

public class ContentTypeAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IContentTypeAdminAppService _contentTypeAppService;

    public ContentTypeAdminAppService_Tests()
    {
        _contentTypeAppService = GetRequiredService<IContentTypeAdminAppService>();
    }

    [Fact]
    public async Task Should_Get_Seeded_Content_Type_With_Its_Field_Arrangement()
    {
        var contentType = await _contentTypeAppService.GetAsync(SiteTestData.PostArticleTypeId);

        contentType.Name.ShouldBe("post-article");
        contentType.PageId.ShouldBe(SiteTestData.BlogPageId);
        contentType.Fields.Count.ShouldBe(5);

        // Order 0 is title, required and searchable - the per-usage flags the design doc calls out as
        // living on the reference, not the field definition (总体设计 §2.3).
        var title = contentType.Fields.Single(f => f.FieldId == SiteTestData.TitleFieldId);
        title.Required.ShouldBeTrue();
        title.Searchable.ShouldBeTrue();
        title.Order.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Round_Trip_The_Field_Arrangement_Through_Create()
    {
        var fields = new List<ContentTypeFieldDto>
        {
            new() { FieldId = SiteTestData.BodyFieldId, Order = 1, ShowInList = false },
            new() { FieldId = SiteTestData.TitleFieldId, Required = true, Searchable = true, Order = 0, DisplayName = "Headline" }
        };

        var created = await _contentTypeAppService.CreateAsync(new CreateContentTypeDto
        {
            PageId = SiteTestData.BlogPageId,
            Name = "admin-arrangement-test",
            DisplayName = "Arrangement Test",
            Fields = fields
        });

        // SetFields sorts by Order - title (order 0) comes first even though it was given second.
        created.Fields.Count.ShouldBe(2);
        created.Fields[0].FieldId.ShouldBe(SiteTestData.TitleFieldId);
        created.Fields[0].DisplayName.ShouldBe("Headline");
        created.Fields[1].FieldId.ShouldBe(SiteTestData.BodyFieldId);
    }

    /// <summary>
    /// End-to-end proof that <c>SchemaType</c>/<c>Fields[].SchemaProperty</c> (GitHub issue #20) survive
    /// the DTO round trip - <c>ContentTypeManager_Tests</c> already covers the domain layer directly, this
    /// covers the mapping code (<c>ContentTypeFieldDtoExtensions</c>, the Mapperly-generated
    /// <c>ContentType</c> → <c>ContentTypeDto</c> mapper) one layer up.
    /// </summary>
    [Fact]
    public async Task Should_Round_Trip_SchemaType_And_SchemaProperty_Through_Create_And_Update()
    {
        var created = await _contentTypeAppService.CreateAsync(new CreateContentTypeDto
        {
            PageId = SiteTestData.BlogPageId,
            Name = "admin-schema-org-test",
            DisplayName = "Schema.org Test",
            SchemaType = SchemaOrgType.Article,
            Fields = new List<ContentTypeFieldDto>
            {
                new() { FieldId = SiteTestData.TitleFieldId, Order = 0, SchemaProperty = "headline" }
            }
        });

        created.SchemaType.ShouldBe(SchemaOrgType.Article);
        created.Fields.Single().SchemaProperty.ShouldBe("headline");

        var updated = await _contentTypeAppService.UpdateAsync(created.Id, new UpdateContentTypeDto
        {
            Name = created.Name,
            DisplayName = created.DisplayName,
            SchemaType = null // leaves the Article mapping in place
        });

        updated.SchemaType.ShouldBe(SchemaOrgType.Article);

        var reloaded = await _contentTypeAppService.GetAsync(created.Id);
        reloaded.SchemaType.ShouldBe(SchemaOrgType.Article);
        reloaded.Fields.Single().SchemaProperty.ShouldBe("headline");
    }

    [Fact]
    public async Task Should_Reject_Deleting_A_Content_Type_That_Still_Has_Contents()
    {
        await Should.ThrowAsync<UserFriendlyException>(
            () => _contentTypeAppService.DeleteAsync(SiteTestData.PostArticleTypeId));
    }

    [Fact]
    public async Task Should_Delete_A_Content_Type_With_No_Contents()
    {
        var created = await _contentTypeAppService.CreateAsync(new CreateContentTypeDto
        {
            PageId = SiteTestData.BlogPageId,
            Name = "admin-empty-delete-test",
            DisplayName = "Empty"
        });

        await _contentTypeAppService.DeleteAsync(created.Id);

        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(
            () => _contentTypeAppService.GetAsync(created.Id));
    }
}
