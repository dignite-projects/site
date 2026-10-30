using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Templates;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.Site.Admin.Templates;

public class SiteTemplateAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ISiteTemplateAdminAppService _templateAppService;

    public SiteTemplateAdminAppService_Tests()
    {
        _templateAppService = GetRequiredService<ISiteTemplateAdminAppService>();
    }

    [Fact]
    public async Task Should_Create_A_Template_And_Publish_Its_First_Version()
    {
        var created = await _templateAppService.CreateAsync(new CreateSiteTemplateDto
        {
            Name = "Admin/Create",
            Body = "<h1>{{ page.name }}</h1>",
            Comment = "first"
        });

        created.Name.ShouldBe("Admin/Create");
        created.PublishedVersionNumber.ShouldBe(1);
        created.LatestVersionNumber.ShouldBe(1);

        var version = await _templateAppService.GetVersionAsync(created.Id, 1);
        version.Body.ShouldBe("<h1>{{ page.name }}</h1>");
        version.Comment.ShouldBe("first");
    }

    [Fact]
    public async Task Should_Leave_The_Pointer_Empty_When_Created_Without_Publishing()
    {
        var created = await _templateAppService.CreateAsync(new CreateSiteTemplateDto
        {
            Name = "Admin/Staged",
            Body = "staged",
            Publish = false
        });

        created.PublishedVersionNumber.ShouldBeNull();
        created.LatestVersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Reject_A_Duplicate_Name()
    {
        await Should.ThrowAsync<SiteTemplateNameAlreadyExistException>(() => _templateAppService.CreateAsync(
            new CreateSiteTemplateDto { Name = SiteTestData.PublishedTemplateName, Body = "x" }));
    }

    [Fact]
    public async Task Should_Reject_A_Name_With_An_Invalid_Format()
    {
        await Should.ThrowAsync<AbpValidationException>(() => _templateAppService.CreateAsync(
            new CreateSiteTemplateDto { Name = "Not Valid", Body = "x" }));
    }

    [Fact]
    public async Task Should_Reject_A_Syntax_Error_With_Its_Position_And_Store_Nothing()
    {
        var exception = await Should.ThrowAsync<SiteTemplateSyntaxException>(() => _templateAppService.CreateAsync(
            new CreateSiteTemplateDto { Name = "Admin/Broken", Body = "line one\n{{ if page.name }} never closed" }));

        exception.Errors.ShouldNotBeEmpty();
        exception.Errors.ShouldAllBe(error => error.Line >= 1 && error.Column >= 1);

        (await _templateAppService.FindByNameAsync("Admin/Broken")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Append_A_Version_To_A_Published_Template_And_Publish_It()
    {
        var updated = await _templateAppService.UpdateAsync(
            SiteTestData.PublishedTemplateId,
            new UpdateSiteTemplateDto { Body = "v3 {{ page.name }}", Comment = "third" });

        updated.LatestVersionNumber.ShouldBe(3);
        updated.PublishedVersionNumber.ShouldBe(3);

        var version = await _templateAppService.GetVersionAsync(SiteTestData.PublishedTemplateId, 3);
        version.Body.ShouldBe("v3 {{ page.name }}");
    }

    [Fact]
    public async Task Should_Stage_A_Version_Without_Changing_What_Is_Published()
    {
        var updated = await _templateAppService.UpdateAsync(
            SiteTestData.PublishedTemplateId,
            new UpdateSiteTemplateDto { Body = "v3 {{ page.name }}", Publish = false });

        updated.LatestVersionNumber.ShouldBe(3);
        updated.PublishedVersionNumber.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Reject_A_Syntax_Error_On_Update_And_Keep_The_Versions()
    {
        await Should.ThrowAsync<SiteTemplateSyntaxException>(() => _templateAppService.UpdateAsync(
            SiteTestData.PublishedTemplateId,
            new UpdateSiteTemplateDto { Body = "{{ if page.name }} never closed" }));

        var template = await _templateAppService.GetAsync(SiteTestData.PublishedTemplateId);
        template.LatestVersionNumber.ShouldBe(2);
        template.PublishedVersionNumber.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Roll_Back_By_Publishing_An_Older_Version()
    {
        var rolledBack = await _templateAppService.PublishAsync(SiteTestData.PublishedTemplateId, 1);

        rolledBack.PublishedVersionNumber.ShouldBe(1);
        rolledBack.LatestVersionNumber.ShouldBe(2);

        (await _templateAppService.GetAsync(SiteTestData.PublishedTemplateId)).PublishedVersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Refuse_To_Publish_A_Version_That_Does_Not_Exist()
    {
        await Should.ThrowAsync<SiteTemplateVersionNotFoundException>(
            () => _templateAppService.PublishAsync(SiteTestData.PublishedTemplateId, 9));
    }

    [Fact]
    public async Task Should_List_Versions_Newest_First_Without_Bodies()
    {
        var versions = await _templateAppService.GetVersionsAsync(SiteTestData.PublishedTemplateId);

        versions.Items.Select(v => v.Number).ShouldBe(new[] { 2, 1 });
        versions.Items.ShouldAllBe(v => v.Body == string.Empty);
        versions.Items.ShouldAllBe(v => v.TemplateId == SiteTestData.PublishedTemplateId);
    }

    [Fact]
    public async Task Should_Return_The_Body_Of_One_Version()
    {
        var version = await _templateAppService.GetVersionAsync(SiteTestData.PublishedTemplateId, 1);

        version.Number.ShouldBe(1);
        version.Body.ShouldBe("v1 {{ page.name }}");
    }

    [Fact]
    public async Task Should_Throw_Not_Found_For_A_Missing_Version()
    {
        await Should.ThrowAsync<EntityNotFoundException>(
            () => _templateAppService.GetVersionAsync(SiteTestData.PublishedTemplateId, 9));
    }

    [Fact]
    public async Task Should_Delete_A_Template()
    {
        await _templateAppService.DeleteAsync(SiteTestData.DraftTemplateId);

        await Should.ThrowAsync<EntityNotFoundException>(() => _templateAppService.GetAsync(SiteTestData.DraftTemplateId));
        (await _templateAppService.FindByNameAsync(SiteTestData.DraftTemplateName)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Free_The_Name_After_Deleting_A_Template()
    {
        await _templateAppService.DeleteAsync(SiteTestData.DraftTemplateId);

        var recreated = await _templateAppService.CreateAsync(
            new CreateSiteTemplateDto { Name = SiteTestData.DraftTemplateName, Body = "again" });

        recreated.LatestVersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Find_By_Name_After_Normalizing_It()
    {
        var found = await _templateAppService.FindByNameAsync("/" + SiteTestData.PublishedTemplateName);

        found.ShouldNotBeNull();
        found.Id.ShouldBe(SiteTestData.PublishedTemplateId);
        found.PublishedVersionNumber.ShouldBe(2);
        found.LatestVersionNumber.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Return_Null_For_An_Unknown_Name()
    {
        (await _templateAppService.FindByNameAsync("does/not-exist")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_List_With_Latest_Version_Numbers_And_Filter_By_Name()
    {
        var all = await _templateAppService.GetListAsync(new GetSiteTemplateListInput());

        var published = all.Items.Single(t => t.Name == SiteTestData.PublishedTemplateName);
        published.PublishedVersionNumber.ShouldBe(2);
        published.LatestVersionNumber.ShouldBe(2);

        var draft = all.Items.Single(t => t.Name == SiteTestData.DraftTemplateName);
        draft.PublishedVersionNumber.ShouldBeNull();
        draft.LatestVersionNumber.ShouldBe(1);

        var filtered = await _templateAppService.GetListAsync(new GetSiteTemplateListInput { Filter = "draft" });
        filtered.Items.Select(t => t.Name).ShouldBe(new[] { SiteTestData.DraftTemplateName });
    }

    [Fact]
    public async Task Should_Require_Site_Head_In_The_Layout()
    {
        await Should.ThrowAsync<SiteTemplateMissingSiteHeadException>(() => _templateAppService.CreateAsync(
            new CreateSiteTemplateDto { Name = SiteTemplateConsts.LayoutName, Body = "<html>{{ body }}</html>" }));

        var layout = await _templateAppService.CreateAsync(new CreateSiteTemplateDto
        {
            Name = SiteTemplateConsts.LayoutName,
            Body = "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>"
        });

        layout.PublishedVersionNumber.ShouldBe(1);
    }
}
