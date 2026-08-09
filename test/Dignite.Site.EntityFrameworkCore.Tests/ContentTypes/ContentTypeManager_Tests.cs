using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Dignite.Site.ContentTypes;

public class ContentTypeManager_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ContentTypeManager _contentTypeManager;
    private readonly IContentTypeRepository _contentTypeRepository;

    public ContentTypeManager_Tests()
    {
        _contentTypeManager = GetRequiredService<ContentTypeManager>();
        _contentTypeRepository = GetRequiredService<IContentTypeRepository>();
    }

    /// <summary>
    /// Regression guard for the field list's value comparer. The arrangement is persisted through a
    /// value converter, and a converted collection with no comparer is snapshotted <i>by reference</i> -
    /// so the "original" EF diffs against is the same list it is diffing, and the edit never reaches the
    /// database. It fails silently: the call succeeds and the old arrangement is still there on reload.
    /// </summary>
    [Fact]
    public async Task Should_Persist_A_Changed_Field_Arrangement()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var contentType = await _contentTypeRepository.GetAsync(SiteTestData.PostGalleryTypeId);

            await _contentTypeManager.UpdateAsync(
                contentType, contentType.Name, contentType.DisplayName, contentType.Description,
                new[]
                {
                    new ContentTypeField(SiteTestData.TitleFieldId, required: true, order: 0),
                    new ContentTypeField(SiteTestData.BodyFieldId, order: 1),
                    new ContentTypeField(SiteTestData.CategoryFieldId, searchable: true, order: 2)
                });
        });

        var reloaded = await WithUnitOfWorkAsync(() =>
            _contentTypeRepository.GetAsync(SiteTestData.PostGalleryTypeId));

        reloaded.Fields.Count.ShouldBe(3);
        reloaded.GetFieldIds().ShouldContain(SiteTestData.CategoryFieldId);
        reloaded.Fields.Single(f => f.FieldId == SiteTestData.TitleFieldId).Required.ShouldBeTrue();
    }

    /// <summary>
    /// Flipping Searchable on has to reindex the contents that already exist.
    /// <para>
    /// Without it the change would take effect only for contents saved afterwards: body is not indexed
    /// today, so my-trip has no index row for it, and marking body searchable would leave that content
    /// invisible to a filter on body. It fails silently - an incomplete result set, not an error - which
    /// is why this is worth a test rather than trust.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Should_Reindex_Existing_Contents_When_A_Field_Becomes_Searchable()
    {
        var indexRepository = GetRequiredService<
            Volo.Abp.Domain.Repositories.IRepository<Contents.ContentFlexFieldIndex, Guid>>();

        var before = await WithUnitOfWorkAsync(() =>
            indexRepository.GetListAsync(x => x.FieldId == SiteTestData.BodyFieldId));
        before.ShouldBeEmpty();

        await WithUnitOfWorkAsync(async () =>
        {
            var contentType = await _contentTypeRepository.GetAsync(SiteTestData.PostArticleTypeId);

            await _contentTypeManager.UpdateAsync(
                contentType, contentType.Name, contentType.DisplayName, contentType.Description,
                contentType.Fields
                    .Select(f => new ContentTypeField(
                        f.FieldId,
                        f.Required,
                        // the flip under test
                        searchable: f.FieldId == SiteTestData.BodyFieldId || f.Searchable,
                        f.ShowInList,
                        f.DisplayName,
                        f.Order))
                    .ToList());
        });

        var after = await WithUnitOfWorkAsync(() =>
            indexRepository.GetListAsync(x => x.FieldId == SiteTestData.BodyFieldId));

        after.ShouldNotBeEmpty();
        after.Select(r => r.StringValue).ShouldContain("Trip body");
    }

    /// <summary>
    /// The arrangement is stored in the order given, sorted by Order - it is what an editor form and an
    /// MCP tool description are both built from.
    /// </summary>
    [Fact]
    public async Task Should_Store_Fields_In_Order()
    {
        var contentType = await WithUnitOfWorkAsync(() => _contentTypeManager.CreateAsync(
            SiteTestData.BlogPageId, "ordered-type", "Ordered type",
            fields: new[]
            {
                new ContentTypeField(SiteTestData.BodyFieldId, order: 5),
                new ContentTypeField(SiteTestData.TitleFieldId, order: 1)
            }));

        contentType.Fields.Select(f => f.FieldId)
            .ShouldBe(new[] { SiteTestData.TitleFieldId, SiteTestData.BodyFieldId });
    }

    /// <summary>
    /// Both usages would resolve against one bag key, so the value would surface twice with possibly
    /// conflicting Required flags and index twice.
    /// </summary>
    [Fact]
    public async Task Should_Reject_A_Field_Referenced_Twice()
    {
        await Should.ThrowAsync<ContentTypeFieldDuplicatedException>(() => WithUnitOfWorkAsync(() =>
            _contentTypeManager.CreateAsync(
                SiteTestData.BlogPageId, "duplicated-field-type", "Duplicated",
                fields: new[]
                {
                    new ContentTypeField(SiteTestData.TitleFieldId, order: 0),
                    new ContentTypeField(SiteTestData.TitleFieldId, order: 1)
                })));
    }

    /// <summary>
    /// Names are unique within the owning page, not tenant-wide - types do not cross pages, so two pages
    /// may each have a type called "post".
    /// </summary>
    [Fact]
    public async Task Should_Scope_Name_Uniqueness_To_The_Page()
    {
        await Should.ThrowAsync<ContentTypeNameAlreadyExistException>(() => WithUnitOfWorkAsync(() =>
            _contentTypeManager.CreateAsync(SiteTestData.BlogPageId, "post-article", "Clashes")));

        var onAnotherPage = await WithUnitOfWorkAsync(() =>
            _contentTypeManager.CreateAsync(SiteTestData.NewsPageId, "post-article", "Fine here"));

        onAnotherPage.Id.ShouldNotBe(Guid.Empty);
    }
}
