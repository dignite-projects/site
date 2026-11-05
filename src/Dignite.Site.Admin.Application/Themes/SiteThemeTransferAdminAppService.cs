using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;

namespace Dignite.Site.Admin.Themes;

/// <inheritdoc cref="ISiteThemeTransferAdminAppService"/>
[Authorize(SiteAdminPermissions.Themes.Default)]
public class SiteThemeTransferAdminAppService : SiteAdminAppService, ISiteThemeTransferAdminAppService
{
    public const string ZipContentType = "application/zip";

    protected SiteThemeManager ThemeManager { get; }
    protected SiteThemeTransferTicketProtector TicketProtector { get; }

    public SiteThemeTransferAdminAppService(SiteThemeManager themeManager, SiteThemeTransferTicketProtector ticketProtector)
    {
        ThemeManager = themeManager;
        TicketProtector = ticketProtector;
    }

    public virtual async Task<IRemoteStreamContent> ExportAsync(Guid? releaseId = null)
    {
        var cancellationToken = RequestCancellationToken;
        var theme = await ThemeManager.GetOrCreateAsync(cancellationToken);
        var (files, defaultLayout) = await ThemeManager.GetFilesForExportAsync(theme, releaseId, cancellationToken);

        string source;
        string fileName;
        if (releaseId == null)
        {
            source = "working copy";
            fileName = "theme-working-copy.zip";
        }
        else
        {
            var release = await ThemeManager.GetReleaseAsync(theme, releaseId.Value, cancellationToken);
            source = $"release {release.Number}";
            fileName = $"theme-release-{release.Number}.zip";
        }

        var zip = SiteThemePackage.Write(files, defaultLayout, source, Clock.Now);
        return new RemoteStreamContent(new MemoryStream(zip), fileName, ZipContentType, zip.Length);
    }

    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task<SiteThemeImportResultDto> ImportAsync(ImportSiteThemeInput input)
    {
        Check.NotNull(input.File, nameof(input.File));

        var cancellationToken = RequestCancellationToken;
        SiteThemePackageContent package;
        // Disposed as the content says: a request body lent by an import link stays open (disposeStream: false).
        using (input.File)
        {
            package = SiteThemePackage.Read(await BufferAsync(input.File.GetStream()));
        }

        var theme = await ThemeManager.GetOrCreateAsync(cancellationToken);
        var import = await ThemeManager.ImportAsync(theme, package, input.Replace, cancellationToken);

        var result = new SiteThemeImportResultDto
        {
            Added = import.Added,
            Modified = import.Modified,
            Deleted = import.Deleted,
            Unchanged = import.Unchanged,
            DefaultLayoutSet = import.DefaultLayoutSet
        };

        foreach (var path in import.Added.Concat(import.Modified).Where(SiteThemePaths.IsTemplate))
        {
            var errors = ScribanTemplateSyntax.GetErrors(package.Files[path])
                .Select(error => new SiteThemeSyntaxErrorDto { Line = error.Line, Column = error.Column, Message = error.Message })
                .ToList();
            if (errors.Count > 0)
            {
                result.SyntaxErrors[path] = errors;
            }
        }

        return result;
    }

    public virtual async Task<SiteThemeTransferLinkDto> CreateExportLinkAsync(CreateSiteThemeExportLinkInput input)
    {
        if (input.ReleaseId != null)
        {
            // Refused now rather than when the link is used: a release of another tenant, or none, is not found.
            var theme = await ThemeManager.GetOrCreateAsync(RequestCancellationToken);
            await ThemeManager.GetReleaseAsync(theme, input.ReleaseId.Value, RequestCancellationToken);
        }

        return Issue(isUpload: false, input.ReleaseId, replace: false);
    }

    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual Task<SiteThemeTransferLinkDto> CreateImportLinkAsync(CreateSiteThemeImportLinkInput input)
    {
        return Task.FromResult(Issue(isUpload: true, releaseId: null, input.Replace));
    }

    protected virtual SiteThemeTransferLinkDto Issue(bool isUpload, Guid? releaseId, bool replace)
    {
        var expiresAt = Clock.Now.Add(SiteThemeTransferTicketProtector.Lifetime);
        var ticket = new SiteThemeTransferTicket(
            SiteThemeTransferTicket.CurrentVersion,
            Guid.NewGuid().ToString("N"),
            new DateTimeOffset(expiresAt),
            CurrentTenant.Id,
            CurrentUser.Id,
            CurrentUser.UserName,
            CurrentUser.Roles,
            CurrentUser.FindClaimValue(AbpClaimTypes.ClientId),
            isUpload,
            releaseId,
            replace);

        var protectedTicket = TicketProtector.Protect(ticket);
        return new SiteThemeTransferLinkDto
        {
            Path = isUpload
                ? SiteThemeTransferPaths.BuildUpload(protectedTicket)
                : SiteThemeTransferPaths.BuildDownload(protectedTicket),
            ExpiresAt = expiresAt
        };
    }

    /// <summary>
    /// The package in memory, no larger than <see cref="SiteThemePackage.MaxBytes"/>: a zip is read by seeking, which a
    /// request body cannot do.
    /// </summary>
    protected static async Task<MemoryStream> BufferAsync(Stream stream)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > SiteThemePackage.MaxBytes)
            {
                throw new BusinessException(SiteErrorCodes.SiteThemePackageInvalid)
                    .WithData("Problems", $"The package is more than {SiteThemePackage.MaxBytes} bytes.");
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return buffer;
    }
}

/// <inheritdoc cref="ISiteThemeTransferTicketService"/>
/// <remarks>
/// The transfer itself is <see cref="ISiteThemeTransferAdminAppService"/>, called with the identity and the tenant the
/// ticket carries: the theme permissions apply as they do to a user with a token. The ticket decides only who and which
/// way; the caller of the endpoint decides nothing.
/// </remarks>
public class SiteThemeTransferTicketService : ISiteThemeTransferTicketService, ITransientDependency
{
    protected SiteThemeTransferTicketProtector TicketProtector { get; }
    protected ISiteThemeTransferAdminAppService TransferAppService { get; }
    protected ICurrentPrincipalAccessor CurrentPrincipalAccessor { get; }
    protected ICurrentTenant CurrentTenant { get; }

    public SiteThemeTransferTicketService(
        SiteThemeTransferTicketProtector ticketProtector,
        ISiteThemeTransferAdminAppService transferAppService,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        ICurrentTenant currentTenant)
    {
        TicketProtector = ticketProtector;
        TransferAppService = transferAppService;
        CurrentPrincipalAccessor = currentPrincipalAccessor;
        CurrentTenant = currentTenant;
    }

    public virtual async Task<IRemoteStreamContent> DownloadAsync(string ticket)
    {
        var read = await RedeemAsync(ticket, isUpload: false);

        using (CurrentPrincipalAccessor.Change(read.ToPrincipal()))
        using (CurrentTenant.Change(read.TenantId))
        {
            return await TransferAppService.ExportAsync(read.ReleaseId);
        }
    }

    public virtual async Task<SiteThemeImportResultDto> UploadAsync(string ticket, Func<long, Stream> openBody)
    {
        var read = await RedeemAsync(ticket, isUpload: true);
        var body = openBody(SiteThemePackage.MaxBytes);

        using (CurrentPrincipalAccessor.Change(read.ToPrincipal()))
        using (CurrentTenant.Change(read.TenantId))
        {
            return await TransferAppService.ImportAsync(new ImportSiteThemeInput
            {
                // Lent, not given: the body is the request's, so the content must not close it.
                File = new RemoteStreamContent(body, "theme.zip", SiteThemeTransferAdminAppService.ZipContentType, disposeStream: false),
                Replace = read.Replace
            });
        }
    }

    /// <summary>The ticket, spent before anything is done with it: two requests racing with one link transfer once.</summary>
    protected virtual async Task<SiteThemeTransferTicket> RedeemAsync(string ticket, bool isUpload)
    {
        var read = TicketProtector.Read(ticket);
        if (read == null || read.IsUpload != isUpload || !await TicketProtector.TryUseAsync(read))
        {
            throw new BusinessException(SiteErrorCodes.SiteThemeTransferLinkInvalid);
        }

        return read;
    }
}
