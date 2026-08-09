using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.Admin.Schema;
using Dignite.Site.ContentTypes;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dignite.Site.Contents;

/// <summary>
/// GitHub issue #43: the CKEditor field type stores and returns a plain string verbatim - no
/// sanitization, no rewriting - so an AI client can write an `&lt;img src="..." alt="..."&gt;` straight
/// into the HTML (总体设计 §5.1 principle 4, §5.9) with no file-upload pipeline involved.
/// </summary>
public class CKEditorField_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IRepository<Field, Guid> _fieldRepository;
    private readonly IRepository<ContentType, Guid> _contentTypeRepository;
    private readonly IContentRepository _contentRepository;
    private readonly ContentManager _contentManager;
    private readonly ISiteSchemaAdminAppService _schemaAppService;

    public CKEditorField_Tests()
    {
        _fieldRepository = GetRequiredService<IRepository<Field, Guid>>();
        _contentTypeRepository = GetRequiredService<IRepository<ContentType, Guid>>();
        _contentRepository = GetRequiredService<IContentRepository>();
        _contentManager = GetRequiredService<ContentManager>();
        _schemaAppService = GetRequiredService<ISiteSchemaAdminAppService>();
    }

    [Fact]
    public async Task Should_Round_Trip_Html_With_A_Real_Image_Tag_Unchanged()
    {
        const string html =
            "<p>Trip report.</p><img src=\"https://cdn.example.com/trip.jpg\" alt=\"Sunset over the harbor\"><p>More text.</p>";

        var fieldName = await WithUnitOfWorkAsync(async () =>
        {
            var (name, contentTypeId) = await CreateCKEditorFieldAndContentTypeAsync();

            var content = await _contentManager.CreateAsync(
                contentTypeId, SiteTestData.EnglishCulture, "richtext-image-post",
                SiteTestData.PublishTime, ContentStatus.Published,
                new Dictionary<string, object?> { [name] = html });

            content.GetField<string>(name).ShouldBe(html);
            return name;
        });

        var reread = await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, "richtext-image-post"));

        reread.ShouldNotBeNull();
        reread!.GetField<string>(fieldName).ShouldBe(html);
    }

    /// <summary>
    /// The other half of #43's acceptance criteria: a content type that pulls the field in has to be
    /// visible to an MCP client through the same schema document #25/#26 already deliver everything else
    /// through - no special-casing by field type (#27's closing comment).
    /// </summary>
    [Fact]
    public async Task Should_Expose_The_Field_In_The_Site_Schema()
    {
        var (fieldName, contentTypeName) = await WithUnitOfWorkAsync(async () =>
        {
            var (name, contentTypeId) = await CreateCKEditorFieldAndContentTypeAsync();
            var contentType = await _contentTypeRepository.GetAsync(contentTypeId);
            return (name, contentType.Name);
        });

        var schema = await _schemaAppService.GetAsync();

        var richTextType = schema.Pages
            .Single(page => page.Name == "blog").ContentTypes
            .Single(ct => ct.Name == contentTypeName);

        richTextType.Fields.Single(f => f.Name == fieldName).FieldTypeName.ShouldBe("CKEditor");
    }

    /// <summary>
    /// Field.Name is tenant-unique and ContentType.Name is page-unique (总体设计 §2.5), and this test
    /// class's two facts share one seeded database (same pattern as
    /// FieldAdminAppService_Tests.Should_Rename_Field_And_Keep_Existing_Values_Reachable...) - so each
    /// call needs its own names, not the fixed ones a one-shot script could get away with.
    /// </summary>
    private async Task<(string FieldName, Guid ContentTypeId)> CreateCKEditorFieldAndContentTypeAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        var fieldName = $"ckeditor-body-{token}";

        var field = await _fieldRepository.InsertAsync(
            new Field(Guid.NewGuid(), fieldName, "Body (rich text)", "CKEditor"), autoSave: true);

        var contentType = await _contentTypeRepository.InsertAsync(
            new ContentType(
                Guid.NewGuid(), SiteTestData.BlogPageId, $"post-richtext-{token}", "Rich text post",
                fields: new[]
                {
                    new ContentTypeField(
                        field.Id, required: false, searchable: false, showInList: true,
                        displayName: null, order: 0)
                }),
            autoSave: true);

        return (fieldName, contentType.Id);
    }
}
