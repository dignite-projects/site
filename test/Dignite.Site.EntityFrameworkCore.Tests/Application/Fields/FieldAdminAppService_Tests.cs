using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Site.Admin.Contents;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.Site.Admin.Fields;

public class FieldAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IFieldAdminAppService _fieldAppService;
    private readonly IContentAdminAppService _contentAppService;
    private readonly IContentRepository _contentRepository;

    public FieldAdminAppService_Tests()
    {
        _fieldAppService = GetRequiredService<IFieldAdminAppService>();
        _contentAppService = GetRequiredService<IContentAdminAppService>();
        _contentRepository = GetRequiredService<IContentRepository>();
    }

    [Fact]
    public async Task Should_Get_Seeded_Field()
    {
        var field = await _fieldAppService.GetAsync(SiteTestData.TitleFieldId);

        field.Name.ShouldBe("title");
        field.FieldTypeName.ShouldBe("Text");
    }

    [Fact]
    public async Task Should_List_Field_Types_Registered_With_The_Kernel()
    {
        var fieldTypes = await _fieldAppService.GetFieldTypesAsync();

        var names = fieldTypes.Items.Select(ft => ft.Name).ToList();
        names.ShouldContain("Text");
        names.ShouldContain("Number");
        names.ShouldContain("DateTime");
        names.ShouldContain("Select");
        names.ShouldContain("Boolean");
        names.ShouldContain("Tree");
        names.ShouldContain("CKEditor");
        names.ShouldContain("FileExplorer");
    }

    /// <summary>
    /// Seo's value is a fixed composite object, not admin-configured like Matrix's block types, so its
    /// keys cannot come from <c>Configuration</c> - they come from <c>IHasValueShape</c> via this catalog
    /// instead. A scalar field type (Text) and a composite-but-dynamically-shaped one (Matrix, whose own
    /// sub-fields already live in its <c>Configuration</c>) both report no value shape at all.
    /// </summary>
    [Fact]
    public async Task Should_Report_Value_Shape_Only_For_Field_Types_With_A_Fixed_Composite_Value()
    {
        var fieldTypes = await _fieldAppService.GetFieldTypesAsync();
        var byName = fieldTypes.Items.ToDictionary(ft => ft.Name);

        var seoShape = byName["Seo"].ValueShape;
        seoShape.ShouldNotBeNull();
        seoShape!.Select(p => p.Name).ShouldBe(new[] { "metaTitle", "metaDescription", "ogImage", "noIndex" });
        seoShape.ShouldAllBe(p => !string.IsNullOrWhiteSpace(p.Description));

        byName["Text"].ValueShape.ShouldBeNull();
        byName["Matrix"].ValueShape.ShouldBeNull();
    }

    /// <summary>
    /// <c>Matrix</c>/<c>Table</c> moved from Site's own <c>Dignite.FlexFields.Site</c> to the flex-fields
    /// kernel at 10.0.0-rc.16 (see CHANGELOG.md); this repo's own test suite never runs abp-modules' own
    /// tests (see ci.yml's "Run domain tests" step comment), so a regression in the kernel's port
    /// wouldn't otherwise get a CI signal here at all. This doesn't re-test the kernel's own
    /// Normalize/Validate internals - that's abp-modules' job - it proves Site's own integration point
    /// still works: <see cref="Dignite.Site.Admin.Fields.FieldAdminAppService.GetFieldTypesAsync"/>'s
    /// fully-qualified <c>fieldType is Dignite.Abp.FlexFields.ICompositeFieldType</c> check still finds
    /// the kernel's implementation of that interface for both types.
    /// </summary>
    [Fact]
    public async Task Should_Report_Matrix_And_Table_As_Composite()
    {
        var fieldTypes = await _fieldAppService.GetFieldTypesAsync();
        var byName = fieldTypes.Items.ToDictionary(ft => ft.Name);

        byName["Matrix"].Composite.ShouldBeTrue();
        byName["Table"].Composite.ShouldBeTrue();
        byName["Text"].Composite.ShouldBeFalse();
    }

    /// <summary>
    /// Exercises <see cref="Dignite.Site.Fields.FieldManager.CheckNestingDepth"/>'s full chain end to
    /// end - resolve "Matrix" through <c>FieldTypeResolver</c>, cast to the kernel's
    /// <c>Dignite.Abp.FlexFields.ICompositeFieldType</c>, call <c>GetInlineFields</c>, measure depth via
    /// <c>Dignite.Abp.FlexFields.CompositeFieldNesting</c> - for the same reason as
    /// <see cref="Should_Report_Matrix_And_Table_As_Composite"/>: this is Site's own integration point
    /// with the kernel's ported Matrix/Table, not the kernel's own internals. An empty block-type list is
    /// enough to exercise the real cast and the real (zero-iteration) <c>GetInlineFields</c> call without
    /// needing to reconstruct the kernel's full nested JSON configuration shape.
    /// </summary>
    [Fact]
    public async Task Should_Create_A_Matrix_Field_Through_The_Kernels_Composite_Field_Type()
    {
        var created = await _fieldAppService.CreateAsync(new CreateFieldDto
        {
            Name = "matrix-kernel-integration-check",
            DisplayName = "Matrix kernel integration check",
            FieldTypeName = "Matrix",
            Configuration = new Dictionary<string, object?> { ["Matrix.BlockTypes"] = new List<object>() }
        });

        created.FieldTypeName.ShouldBe("Matrix");

        await _fieldAppService.DeleteAsync(created.Id);
    }

    [Fact]
    public async Task Should_Round_Trip_Create_With_Configuration()
    {
        var created = await _fieldAppService.CreateAsync(new CreateFieldDto
        {
            Name = "admin-config-round-trip",
            DisplayName = "Round trip",
            FieldTypeName = "Number",
            Configuration = new Dictionary<string, object?> { ["Min"] = 0, ["Max"] = 100 }
        });

        created.Configuration.ShouldNotBeEmpty();

        var fetched = await _fieldAppService.GetAsync(created.Id);
        JsonSerializer.Serialize(fetched.Configuration).ShouldBe(JsonSerializer.Serialize(created.Configuration));
    }

    /// <summary>
    /// The whole point of routing rename through <c>FieldManager.RenameAsync</c> rather than
    /// <c>Field.SetName</c> directly: the value stored under the old bag key must still be reachable,
    /// now under the new one, for content that already existed before the rename.
    /// </summary>
    [Fact]
    public async Task Should_Rename_Field_And_Keep_Existing_Values_Reachable_Under_The_New_Name()
    {
        var tripContent = await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug));
        tripContent.ShouldNotBeNull();

        var beforeViews = (await _contentAppService.GetAsync(tripContent!.Id)).FieldValues["views"];

        var renamed = await _fieldAppService.RenameAsync(
            SiteTestData.ViewsFieldId, new RenameFieldDto { NewName = "view-count" });

        renamed.Name.ShouldBe("view-count");

        var afterRename = await _contentAppService.GetAsync(tripContent.Id);
        afterRename.FieldValues.ShouldNotContainKey("views");
        afterRename.FieldValues.ShouldContainKey("view-count");
        JsonSerializer.Serialize(afterRename.FieldValues["view-count"]).ShouldBe(JsonSerializer.Serialize(beforeViews));

        // Restore the seed's own name so later tests in this shared database still see "views".
        await _fieldAppService.RenameAsync(SiteTestData.ViewsFieldId, new RenameFieldDto { NewName = "views" });
    }

    [Fact]
    public async Task Should_Reject_A_Name_With_An_Invalid_Format()
    {
        await Should.ThrowAsync<AbpValidationException>(() => _fieldAppService.CreateAsync(new CreateFieldDto
        {
            Name = "Not Valid",
            DisplayName = "Bad name",
            FieldTypeName = "Text"
        }));
    }

    [Fact]
    public async Task Should_Reject_A_Rename_To_A_New_Name_With_An_Invalid_Format()
    {
        await Should.ThrowAsync<AbpValidationException>(() => _fieldAppService.RenameAsync(
            SiteTestData.ViewsFieldId, new RenameFieldDto { NewName = "Not Valid" }));
    }
}
