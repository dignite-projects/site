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
