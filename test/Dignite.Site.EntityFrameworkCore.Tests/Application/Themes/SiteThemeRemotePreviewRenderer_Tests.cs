using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Themes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Http.Client;
using Xunit;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// An administration API deployed apart from the site: no renderer of its own, <c>RemoteServices:SitePublicWeb</c> naming
/// the site's application, which renders the previews and the trial render. Previews and checks behave as they do with a
/// renderer in the same application - the same results, the same errors.
/// </summary>
public class SiteThemeRemotePreviewRenderer_Tests : SiteEntityFrameworkCoreTestBase
{
    private const string SiteAddress = "http://site.internal:8080/";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISiteThemeAdminAppService _themeAppService;
    private readonly FakeSite _site;

    public SiteThemeRemotePreviewRenderer_Tests()
    {
        _themeAppService = GetRequiredService<ISiteThemeAdminAppService>();
        _site = GetRequiredService<FakeSite>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.RemoveAll<ISiteThemePreviewRenderer>();

        services.Configure<AbpRemoteServiceOptions>(options =>
        {
            options.RemoteServices[SiteThemePreviewConsts.RendererRemoteServiceName] = new RemoteServiceConfiguration(SiteAddress);
        });

        services.AddSingleton<FakeSite>();
        services.AddHttpClient(SiteThemeRemotePreviewRenderer.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(provider => provider.GetRequiredService<FakeSite>());
    }

    private Task SaveAsync(string path, string body) =>
        _themeAppService.SaveFileAsync(new SaveSiteThemeFileDto { Path = path, Body = body });

    [Fact]
    public void The_Provider_Should_Pick_The_Site_When_It_Is_Configured()
    {
        GetRequiredService<SiteThemePreviewRendererProvider>().Find().ShouldBeOfType<SiteThemeRemotePreviewRenderer>();
    }

    /// <summary>
    /// The snapshot goes to the site's render endpoint with a service token for the current tenant, and the page comes
    /// back as the preview.
    /// </summary>
    [Fact]
    public async Task A_Preview_Should_Be_Rendered_By_The_Site()
    {
        await SaveAsync("Default", "<p>draft</p>");

        var preview = await _themeAppService.PreviewAsync("/about");

        preview.Html.ShouldBe("<html><p>draft</p></html>");
        preview.Path.ShouldBe("/about");

        var call = _site.Calls.ShouldHaveSingleItem();
        call.Address.ShouldBe(SiteAddress + SiteThemePreviewConsts.RenderPath);
        call.Input.Path.ShouldBe("/about");
        call.Input.Snapshot.Files.Single(file => file.Path == "Default").Body.ShouldBe("<p>draft</p>");

        GetRequiredService<SitePreviewServiceTokenProtector>().TryUnprotect(call.Input.Token, out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBeNull();
        GetRequiredService<SitePreviewTokenProtector>().TryUnprotect(call.Input.Token, out _).ShouldBeFalse();
    }

    /// <summary>The site's business failure is raised here again, code and data, as a local renderer raises it.</summary>
    [Fact]
    public async Task The_Sites_Failure_Should_Be_Raised_Again()
    {
        await SaveAsync("Default", "<p>draft</p>");
        _site.Error = new SiteThemePreviewRenderError
        {
            Code = SiteErrorCodes.SiteTemplatePreviewPathNotFound,
            Data = new Dictionary<string, string?> { ["Path"] = "/nowhere" }
        };

        var exception = await Should.ThrowAsync<BusinessException>(() => _themeAppService.PreviewAsync("/nowhere"));

        exception.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewPathNotFound);
        exception.Data["Path"].ShouldBe("/nowhere");
    }

    /// <summary>The trial render reports a template's failure at its place, the line and column as numbers again.</summary>
    [Fact]
    public async Task The_Check_Should_Report_A_Failure_The_Site_Found_At_Its_Place()
    {
        await SaveAsync("Default", "{{ include \"_header\" title: page.display_name }}");
        await SaveAsync("_header", "<h1>{{ $title }}</h1>");
        _site.Error = new SiteThemePreviewRenderError
        {
            Code = SiteErrorCodes.SiteTemplatePreviewRenderFailed,
            Data = new Dictionary<string, string?>
            {
                ["Name"] = "Default",
                ["Message"] = "_header(1,9) : error : The variable or function $title was not found",
                ["File"] = "_header",
                ["Line"] = "1",
                ["Column"] = "9"
            }
        };

        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeTrue();
        var issue = check.Issues.Single(i => i.Code == SiteThemeIssueCodes.RenderFailed);
        issue.Path.ShouldBe("_header");
        issue.Line.ShouldBe(1);
        issue.Column.ShouldBe(9);
    }

    /// <summary>
    /// A site that does not answer - wrong address, other keys - is the application's failure: the trial render stops and
    /// says so, and does not blame the theme.
    /// </summary>
    [Fact]
    public async Task A_Site_That_Refuses_The_Call_Should_Stop_The_Trial_Render_And_Say_So()
    {
        await SaveAsync("Default", "<p>draft</p>");
        _site.Status = HttpStatusCode.NotFound;

        var check = await _themeAppService.CheckAsync();

        check.HasErrors.ShouldBeFalse();
        check.Issues.ShouldHaveSingleItem().Code.ShouldBe(SiteThemeIssueCodes.TrialRenderStopped);
        _site.Calls.ShouldHaveSingleItem();
    }

    /// <summary>The site's render endpoint, as a message handler: records each call and answers as it is told.</summary>
    public class FakeSite : HttpMessageHandler
    {
        public List<(string Address, SiteThemePreviewRenderInput Input)> Calls { get; } = new();

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public SiteThemePreviewRenderError? Error { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Method.ShouldBe(HttpMethod.Post);
            var input = (await request.Content!.ReadFromJsonAsync<SiteThemePreviewRenderInput>(JsonOptions, cancellationToken))!;
            Calls.Add((request.RequestUri!.ToString(), input));

            if (Status != HttpStatusCode.OK)
            {
                return new HttpResponseMessage(Status);
            }

            var page = input.Snapshot.Files.FirstOrDefault(file => file.Path == "Default")?.Body ?? string.Empty;
            var result = Error != null
                ? new SiteThemePreviewRenderResult { Error = Error }
                : new SiteThemePreviewRenderResult
                {
                    Preview = new SiteThemePreviewDto
                    {
                        Html = "<html>" + page + "</html>",
                        Path = input.Path,
                        PageName = "about",
                        CultureName = "en",
                        PageTemplate = "Default"
                    }
                };

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result, options: JsonOptions) };
        }
    }
}
