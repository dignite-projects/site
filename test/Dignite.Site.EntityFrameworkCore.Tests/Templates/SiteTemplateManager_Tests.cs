using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Dignite.Site.Templates;

public class SiteTemplateManager_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteTemplateManager _templateManager;
    private readonly ISiteTemplateRepository _templateRepository;

    public SiteTemplateManager_Tests()
    {
        _templateManager = GetRequiredService<SiteTemplateManager>();
        _templateRepository = GetRequiredService<ISiteTemplateRepository>();
    }

    [Fact]
    public async Task Should_Publish_The_First_Version_By_Default()
    {
        var created = await WithUnitOfWorkAsync(() =>
            _templateManager.CreateAsync("create-default", "<p>{{ page.name }}</p>", "first"));

        var reloaded = await WithUnitOfWorkAsync(() => _templateRepository.GetAsync(created.Id));

        reloaded.Name.ShouldBe("create-default");
        reloaded.PublishedVersionNumber.ShouldBe(1);
        reloaded.Versions.Count.ShouldBe(1);
        reloaded.Versions.Single().Number.ShouldBe(1);
        reloaded.Versions.Single().Comment.ShouldBe("first");
    }

    [Fact]
    public async Task Should_Leave_A_Template_Created_Without_Publishing_As_A_Draft()
    {
        var created = await WithUnitOfWorkAsync(() =>
            _templateManager.CreateAsync("create-draft", "<p>draft</p>", publish: false));

        var reloaded = await WithUnitOfWorkAsync(() => _templateRepository.GetAsync(created.Id));

        reloaded.PublishedVersionNumber.ShouldBeNull();
        reloaded.Versions.Count.ShouldBe(1);
        (await WithUnitOfWorkAsync(() => _templateRepository.FindPublishedVersionAsync("create-draft")))
            .ShouldBeNull();
    }

    [Fact]
    public async Task Should_Store_The_Name_Normalized()
    {
        var created = await WithUnitOfWorkAsync(() =>
            _templateManager.CreateAsync("/Normalized/Name", "body"));

        created.Name.ShouldBe("Normalized/Name");
    }

    [Fact]
    public async Task Should_Reject_A_Duplicate_Name()
    {
        await Should.ThrowAsync<SiteTemplateNameAlreadyExistException>(() => WithUnitOfWorkAsync(() =>
            _templateManager.CreateAsync(SiteTestData.PublishedTemplateName, "body")));

        // The same name, spelled the way a page would: with the leading slash.
        await Should.ThrowAsync<SiteTemplateNameAlreadyExistException>(() => WithUnitOfWorkAsync(() =>
            _templateManager.CreateAsync("/" + SiteTestData.DraftTemplateName, "body")));
    }

    [Fact]
    public async Task Should_Reject_A_Syntax_Error_And_Persist_Nothing()
    {
        var exception = await Should.ThrowAsync<SiteTemplateSyntaxException>(() => WithUnitOfWorkAsync(() =>
            _templateManager.CreateAsync("broken-template", "{{ if x }}no end")));

        exception.Errors.Count.ShouldBeGreaterThan(0);
        exception.Data["Name"].ShouldBe("broken-template");

        (await WithUnitOfWorkAsync(() => _templateRepository.FindByNameAsync("broken-template"))).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Store_An_Asset_Without_Parsing_It()
    {
        // Not Scriban: the "{{ color: }}" is CSS, and as Scriban it does not even parse (the control test below
        // shows the same body refused under a page name) - an asset is stored without the parse at all.
        var created = await WithUnitOfWorkAsync(() =>
            _templateManager.CreateAsync("assets/site.css", ".a{{ color: }}"));

        created.Name.ShouldBe("assets/site.css");
        created.PublishedVersionNumber.ShouldBe(1);

        var published = await WithUnitOfWorkAsync(() => _templateRepository.FindPublishedVersionAsync("assets/site.css"));
        published.ShouldNotBeNull();
        published.Body.ShouldBe(".a{{ color: }}");
    }

    [Fact]
    public async Task Should_Still_Parse_The_Same_Body_Under_A_Page_Name()
    {
        await Should.ThrowAsync<SiteTemplateSyntaxException>(() => WithUnitOfWorkAsync(() =>
            _templateManager.CreateAsync("not-an-asset", ".a{{ color: }}")));
    }

    [Fact]
    public async Task Should_List_Only_Templates_Under_A_Prefix()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await _templateManager.CreateAsync("partials/list-a", "<i>a</i>");
            await _templateManager.CreateAsync("partials/list-b", "<i>b</i>");
            await _templateManager.CreateAsync("not-partials/list-c", "<i>c</i>");
        });

        var partials = await WithUnitOfWorkAsync(() => _templateRepository.GetListAsync(prefix: "partials/"));
        partials.Select(t => t.Name).ShouldBe(new[] { "partials/list-a", "partials/list-b" });

        // The prefix is normalized like a name, and combines with the free-text filter.
        var filtered = await WithUnitOfWorkAsync(() => _templateRepository.GetListAsync("list-b", "/partials/"));
        filtered.Select(t => t.Name).ShouldBe(new[] { "partials/list-b" });
    }

    [Fact]
    public async Task Should_Require_Site_Head_In_The_Layout()
    {
        await Should.ThrowAsync<SiteTemplateMissingSiteHeadException>(() => WithUnitOfWorkAsync(() =>
            _templateManager.CreateAsync("layout", "<html><head></head><body>{{ body }}</body></html>")));

        (await WithUnitOfWorkAsync(() => _templateRepository.FindByNameAsync("layout"))).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Accept_A_Layout_That_Calls_Site_Head()
    {
        var created = await WithUnitOfWorkAsync(() => _templateManager.CreateAsync(
            "layout", "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>"));

        created.Name.ShouldBe(SiteTemplateConsts.LayoutName);
        created.PublishedVersionNumber.ShouldBe(1);
    }

    [Fact]
    public async Task Should_Hold_Every_Later_Version_Of_The_Layout_To_The_Same_Rule()
    {
        await WithUnitOfWorkAsync(() => _templateManager.CreateAsync(
            "layout", "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>"));

        await Should.ThrowAsync<SiteTemplateMissingSiteHeadException>(() => WithUnitOfWorkAsync(async () =>
        {
            var layout = await _templateRepository.FindByNameAsync("layout");
            await _templateManager.AddVersionAsync(layout!, "<html><body>{{ body }}</body></html>");
        }));
    }

    [Fact]
    public async Task Should_Add_The_Next_Version_And_Publish_It()
    {
        var version = await WithUnitOfWorkAsync(async () =>
        {
            var template = await _templateRepository.GetAsync(SiteTestData.PublishedTemplateId);
            return await _templateManager.AddVersionAsync(template, "v3 {{ page.name }}", "third");
        });

        version.Number.ShouldBe(3);

        var reloaded = await WithUnitOfWorkAsync(() => _templateRepository.GetAsync(SiteTestData.PublishedTemplateId));
        reloaded.Versions.Select(v => v.Number).OrderBy(n => n).ShouldBe(new[] { 1, 2, 3 });
        reloaded.PublishedVersionNumber.ShouldBe(3);
    }

    [Fact]
    public async Task Should_Keep_The_Live_Version_When_A_New_One_Is_Saved_Without_Publishing()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var template = await _templateRepository.GetAsync(SiteTestData.PublishedTemplateId);
            await _templateManager.AddVersionAsync(template, "v3 {{ page.name }}", publish: false);
        });

        var reloaded = await WithUnitOfWorkAsync(() => _templateRepository.GetAsync(SiteTestData.PublishedTemplateId));
        reloaded.Versions.Count.ShouldBe(3);
        reloaded.PublishedVersionNumber.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Reject_A_Syntax_Error_In_A_New_Version_And_Add_Nothing()
    {
        await Should.ThrowAsync<SiteTemplateSyntaxException>(() => WithUnitOfWorkAsync(async () =>
        {
            var template = await _templateRepository.GetAsync(SiteTestData.PublishedTemplateId);
            await _templateManager.AddVersionAsync(template, "{{ for }}");
        }));

        var reloaded = await WithUnitOfWorkAsync(() => _templateRepository.GetAsync(SiteTestData.PublishedTemplateId));
        reloaded.Versions.Count.ShouldBe(2);
        reloaded.PublishedVersionNumber.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Roll_Back_By_Publishing_An_Earlier_Version()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var template = await _templateRepository.GetAsync(SiteTestData.PublishedTemplateId);
            await _templateManager.PublishAsync(template, 1);
        });

        var published = await WithUnitOfWorkAsync(() =>
            _templateRepository.FindPublishedVersionAsync(SiteTestData.PublishedTemplateName));

        published.ShouldNotBeNull();
        published.Number.ShouldBe(1);
        published.Body.ShouldBe("v1 {{ page.name }}");
    }

    [Fact]
    public async Task Should_Reject_Publishing_A_Version_That_Does_Not_Exist()
    {
        await Should.ThrowAsync<SiteTemplateVersionNotFoundException>(() => WithUnitOfWorkAsync(async () =>
        {
            var template = await _templateRepository.GetAsync(SiteTestData.PublishedTemplateId);
            await _templateManager.PublishAsync(template, 9);
        }));
    }

    [Fact]
    public async Task Should_Find_The_Published_Version_By_Name()
    {
        var published = await WithUnitOfWorkAsync(() =>
            _templateRepository.FindPublishedVersionAsync(SiteTestData.PublishedTemplateName));

        published.ShouldNotBeNull();
        published.Number.ShouldBe(2);
        published.Body.ShouldBe("v2 {{ page.name }}");
        published.Comment.ShouldBe("second");
    }

    [Fact]
    public async Task Should_Find_No_Published_Version_For_A_Draft_Or_Unknown_Template()
    {
        (await WithUnitOfWorkAsync(() => _templateRepository.FindPublishedVersionAsync(SiteTestData.DraftTemplateName)))
            .ShouldBeNull();
        (await WithUnitOfWorkAsync(() => _templateRepository.FindPublishedVersionAsync("no-such-template")))
            .ShouldBeNull();
    }

    [Fact]
    public async Task Should_Report_The_Latest_Version_Number_Of_Each_Template()
    {
        var latest = await WithUnitOfWorkAsync(() => _templateRepository.GetLatestVersionNumbersAsync(
            new[] { SiteTestData.PublishedTemplateId, SiteTestData.DraftTemplateId, Guid.NewGuid() }));

        latest.Count.ShouldBe(2);
        latest[SiteTestData.PublishedTemplateId].ShouldBe(2);
        latest[SiteTestData.DraftTemplateId].ShouldBe(1);
    }

    [Fact]
    public async Task Should_List_Templates_By_Name_And_Filter_Ignoring_Case()
    {
        var all = await WithUnitOfWorkAsync(() => _templateRepository.GetListAsync(includeDetails: false));
        all.Select(t => t.Name).ShouldBe(
            new[] { SiteTestData.DraftTemplateName, SiteTestData.PublishedTemplateName });

        var filtered = await WithUnitOfWorkAsync(() => _templateRepository.GetListAsync("PUBLISHED", includeDetails: false));
        filtered.Select(t => t.Name).ShouldBe(new[] { SiteTestData.PublishedTemplateName });
    }

    [Fact]
    public async Task Should_Free_The_Name_Of_A_Deleted_Template()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var template = await _templateRepository.GetAsync(SiteTestData.DraftTemplateId);
            await _templateManager.DeleteAsync(template);
        });

        (await WithUnitOfWorkAsync(() => _templateRepository.FindByNameAsync(SiteTestData.DraftTemplateName)))
            .ShouldBeNull();

        var recreated = await WithUnitOfWorkAsync(() =>
            _templateManager.CreateAsync(SiteTestData.DraftTemplateName, "again"));
        recreated.Id.ShouldNotBe(SiteTestData.DraftTemplateId);
    }
}
