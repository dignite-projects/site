using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.FlexFields.Site.Seo;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Dignite.Site.Seo;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dignite.Site.Fields;

/// <summary>
/// Renaming and deleting a field definition, which are data migrations across every content rather than
/// edits to one row - a field's name <i>is</i> the key its values are stored under (总体设计 §2.4).
/// </summary>
public class FieldManager_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly FieldManager _fieldManager;
    private readonly IFieldRepository _fieldRepository;
    private readonly IContentRepository _contentRepository;
    private readonly IRepository<ContentFlexFieldIndex, Guid> _indexRepository;

    public FieldManager_Tests()
    {
        _fieldManager = GetRequiredService<FieldManager>();
        _fieldRepository = GetRequiredService<IFieldRepository>();
        _contentRepository = GetRequiredService<IContentRepository>();
        _indexRepository = GetRequiredService<IRepository<ContentFlexFieldIndex, Guid>>();
    }

    /// <summary>
    /// The rename has to move the value with the key. Without the migration step the value would still
    /// be in the bag under the old name, where no read path can reach it.
    /// </summary>
    [Fact]
    public async Task Should_Move_Stored_Values_When_Renaming_A_Field()
    {
        var affected = await WithUnitOfWorkAsync(async () =>
        {
            var field = await _fieldRepository.GetAsync(SiteTestData.ViewsFieldId);
            return await _fieldManager.RenameAsync(field, "view-count");
        });

        // my-trip (42) and draft-post (7) both carry a value for this field.
        affected.ShouldBe(2);

        await WithUnitOfWorkAsync(async () =>
        {
            var content = await FindTripAsync();

            content.HasField("view-count").ShouldBeTrue();
            content.HasField("views").ShouldBeFalse();
            Convert.ToDecimal(content.GetField("view-count")).ShouldBe(42);
        });
    }

    /// <summary>
    /// ...and the index needs no rebuild afterwards, because its rows key on the field's id and its
    /// value, neither of which a rename touches. This is the payoff of the two layers keying differently.
    /// </summary>
    [Fact]
    public async Task Should_Leave_Index_Rows_Intact_When_Renaming_A_Field()
    {
        var before = await WithUnitOfWorkAsync(() =>
            _indexRepository.GetListAsync(x => x.FieldId == SiteTestData.CategoryFieldId));

        await WithUnitOfWorkAsync(async () =>
        {
            var field = await _fieldRepository.GetAsync(SiteTestData.CategoryFieldId);
            await _fieldManager.RenameAsync(field, "topic");
        });

        var after = await WithUnitOfWorkAsync(() =>
            _indexRepository.GetListAsync(x => x.FieldId == SiteTestData.CategoryFieldId));

        after.Count.ShouldBe(before.Count);
        after.Select(r => r.StringValue).OrderBy(v => v)
            .ShouldBe(before.Select(r => r.StringValue).OrderBy(v => v));
    }

    /// <summary>
    /// A rename onto a name already in use would collide inside every value bag, so it is refused before
    /// anything is written.
    /// </summary>
    [Fact]
    public async Task Should_Reject_Rename_Onto_An_Existing_Name()
    {
        await Should.ThrowAsync<FieldNameAlreadyExistException>(() => WithUnitOfWorkAsync(async () =>
        {
            var field = await _fieldRepository.GetAsync(SiteTestData.BodyFieldId);
            await _fieldManager.RenameAsync(field, "title");
        }));
    }

    [Fact]
    public async Task Should_Reject_Duplicate_Name_On_Create()
    {
        await Should.ThrowAsync<FieldNameAlreadyExistException>(() => WithUnitOfWorkAsync(() =>
            _fieldManager.CreateAsync("title", "Another title", "Text")));
    }

    /// <summary>
    /// Deleting strips the values out of every bag first. Reversed, they would sit there forever -
    /// unreachable, since nothing names them any more, but still serialized on every read.
    /// </summary>
    [Fact]
    public async Task Should_Strip_Stored_Values_When_Deleting_A_Field()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var field = await _fieldManager.CreateAsync("doomed", "Doomed", "Text");

            var content = await FindTripAsync();
            content.SetField("doomed", "some value");
            await _contentRepository.UpdateAsync(content, autoSave: true);

            await _fieldManager.DeleteAsync(field);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var content = await FindTripAsync();
            content.HasField("doomed").ShouldBeFalse();

            (await _fieldRepository.FindByNameAsync("doomed")).ShouldBeNull();
        });
    }

    private async Task<Content> FindTripAsync()
    {
        return (await _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug))!;
    }

    /// <summary>
    /// The platform preset guard (总体设计 §5.3, GitHub issue #14): deleting the seeded SEO field would
    /// silently disable the noindex semantic for every content type that pulled it in, so FieldManager
    /// refuses outright rather than letting it happen and be discovered later.
    /// </summary>
    [Fact]
    public async Task Should_Reject_Deleting_The_Seo_Preset_Field()
    {
        await Should.ThrowAsync<FieldIsPlatformPresetCannotBeDeletedException>(() => WithUnitOfWorkAsync(async () =>
        {
            var field = await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);
            await _fieldManager.DeleteAsync(field!);
        }));
    }

    /// <summary>
    /// The SEO field's Name is the only thing that makes it recognizable across a shared database (see
    /// <c>SeoFieldNames</c>' remarks) - renaming it away would leave nothing for recognition to find.
    /// </summary>
    [Fact]
    public async Task Should_Reject_Renaming_The_Seo_Preset_Field()
    {
        await Should.ThrowAsync<FieldIsPlatformPresetCannotBeRenamedException>(() => WithUnitOfWorkAsync(async () =>
        {
            var field = await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName);
            await _fieldManager.RenameAsync(field!, "our-seo");
        }));
    }

    /// <summary>
    /// Only the Name is protected - everything else about the preset field is a normal, fully editable
    /// field, so a tenant can still reconfigure display, description and type freely.
    /// </summary>
    [Fact]
    public async Task Should_Allow_Updating_The_Seo_Preset_Fields_Other_Properties()
    {
        var updated = await WithUnitOfWorkAsync(async () =>
        {
            var field = (await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName))!;
            return await _fieldManager.UpdateAsync(field, "Our SEO", field.FieldTypeName, "Custom description");
        });

        updated.Name.ShouldBe(SeoFieldNames.FieldName);
        updated.DisplayName.ShouldBe("Our SEO");
        updated.Description.ShouldBe("Custom description");
    }
}
