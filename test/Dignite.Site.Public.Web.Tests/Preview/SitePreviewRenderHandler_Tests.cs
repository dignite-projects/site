using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.DataProtection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// <c>POST /site-preview/render</c>: the page an administration API deployed apart from the site asks for, rendered for
/// the tenant its token names - and nothing for a call without a valid service token.
/// </summary>
public class SitePreviewRenderHandler_Tests
{
    private static readonly Guid TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly EphemeralDataProtectionProvider _dataProtection = new();
    private readonly CurrentTenant _currentTenant = new(AsyncLocalCurrentTenantAccessor.Instance);
    private readonly RecordingRenderer _renderer;

    public SitePreviewRenderHandler_Tests()
    {
        _renderer = new RecordingRenderer(_currentTenant);
    }

    private SitePreviewServiceTokenProtector ServiceProtector => new(_dataProtection);

    private SitePreviewRenderHandler CreateHandler() => new(ServiceProtector, _renderer, _currentTenant);

    private static SiteThemeSnapshotDto Snapshot() => new()
    {
        Files = new List<SiteThemeSnapshotFileDto> { new() { Path = "Default", ContentHash = "h", Body = "<p>x</p>" } }
    };

    [Fact]
    public async Task Should_Render_The_Snapshot_For_The_Tenant_The_Token_Names()
    {
        var token = ServiceProtector.Protect(new SitePreviewTicket(TenantId, null));

        var result = await CreateHandler().HandleAsync(new SiteThemePreviewRenderInput
        {
            Token = token,
            Path = "/about",
            Snapshot = Snapshot()
        });

        result.ShouldNotBeNull();
        result.Error.ShouldBeNull();
        result.Preview!.Path.ShouldBe("/about");
        _renderer.Request!.Snapshot.Files.ShouldHaveSingleItem().Body.ShouldBe("<p>x</p>");
        _renderer.TenantId.ShouldBe(TenantId);
        _currentTenant.Id.ShouldBeNull();
    }

    /// <summary>The renderer's failure is part of the answer, every value as text, so the caller can raise it again.</summary>
    [Fact]
    public async Task A_Business_Failure_Should_Come_Back_As_Its_Code_And_Data()
    {
        _renderer.Failure = new BusinessException(SiteErrorCodes.SiteTemplatePreviewRenderFailed)
            .WithData("Message", "boom")
            .WithData("File", "_header")
            .WithData("Line", 3)
            .WithData("Column", 9);

        var result = await CreateHandler().HandleAsync(new SiteThemePreviewRenderInput
        {
            Token = ServiceProtector.Protect(new SitePreviewTicket(null, null)),
            Path = "/",
            Snapshot = Snapshot()
        });

        result!.Preview.ShouldBeNull();
        result.Error!.Code.ShouldBe(SiteErrorCodes.SiteTemplatePreviewRenderFailed);
        result.Error.Data["Message"].ShouldBe("boom");
        result.Error.Data["File"].ShouldBe("_header");
        result.Error.Data["Line"].ShouldBe("3");
        result.Error.Data["Column"].ShouldBe("9");
    }

    /// <summary>Anything else is the site's failure, and fails the call.</summary>
    [Fact]
    public async Task Any_Other_Failure_Should_Propagate()
    {
        _renderer.Failure = new InvalidOperationException("the database went away");

        await Should.ThrowAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(new SiteThemePreviewRenderInput
        {
            Token = ServiceProtector.Protect(new SitePreviewTicket(null, null)),
            Path = "/",
            Snapshot = Snapshot()
        }));
    }

    /// <summary>
    /// A browser preview's token - a link that may have leaked - renders nothing here, nor does a token of other keys,
    /// nor nothing at all.
    /// </summary>
    [Fact]
    public async Task Should_Answer_Nothing_Without_A_Valid_Service_Token()
    {
        var browserToken = new SitePreviewTokenProtector(_dataProtection)
            .Protect(new SitePreviewTicket(null, null), TimeSpan.FromMinutes(1), out _);
        var otherKeys = new SitePreviewServiceTokenProtector(new EphemeralDataProtectionProvider())
            .Protect(new SitePreviewTicket(null, null));

        foreach (var token in new[] { browserToken, otherKeys, "garbage", "" })
        {
            (await CreateHandler().HandleAsync(new SiteThemePreviewRenderInput
            {
                Token = token,
                Path = "/",
                Snapshot = Snapshot()
            })).ShouldBeNull();
        }

        (await CreateHandler().HandleAsync(null)).ShouldBeNull();
        _renderer.Request.ShouldBeNull();
    }

    private sealed class RecordingRenderer : ISiteThemePreviewRenderer
    {
        private readonly ICurrentTenant _currentTenant;

        public RecordingRenderer(ICurrentTenant currentTenant)
        {
            _currentTenant = currentTenant;
        }

        public SiteThemePreviewRequest? Request { get; private set; }

        public Guid? TenantId { get; private set; }

        public Exception? Failure { get; set; }

        public Task<SiteThemePreviewDto> RenderAsync(SiteThemePreviewRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            TenantId = _currentTenant.Id;

            if (Failure != null)
            {
                throw Failure;
            }

            return Task.FromResult(new SiteThemePreviewDto
            {
                Html = "<html></html>",
                Path = request.Path,
                PageName = "about",
                CultureName = "en",
                PageTemplate = "Default"
            });
        }
    }
}
