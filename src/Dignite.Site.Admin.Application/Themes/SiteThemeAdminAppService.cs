using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Themes;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Threading;

namespace Dignite.Site.Admin.Themes;

[Authorize(SiteAdminPermissions.Themes.Default)]
public class SiteThemeAdminAppService : SiteAdminAppService, ISiteThemeAdminAppService
{
    protected SiteThemeManager ThemeManager { get; }
    protected ISiteThemeFileRepository FileRepository { get; }
    protected ISiteReleaseRepository ReleaseRepository { get; }
    protected SiteThemeTrialRenderer TrialRenderer { get; }

    public SiteThemeAdminAppService(
        SiteThemeManager themeManager,
        ISiteThemeFileRepository fileRepository,
        ISiteReleaseRepository releaseRepository,
        SiteThemeTrialRenderer trialRenderer)
    {
        ThemeManager = themeManager;
        FileRepository = fileRepository;
        ReleaseRepository = releaseRepository;
        TrialRenderer = trialRenderer;
    }

    public virtual async Task<SiteThemeDto> GetAsync()
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        return await MapToDtoAsync(theme);
    }

    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task<SiteThemeDto> UpdateAsync(UpdateSiteThemeDto input)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        await ThemeManager.SetDefaultLayoutAsync(theme, input.DefaultLayout, input.ConcurrencyStamp);

        return await MapToDtoAsync(theme);
    }

    public virtual async Task<ListResultDto<SiteThemeFileSummaryDto>> GetFilesAsync()
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        var files = await FileRepository.GetSummariesAsync(theme.Id);
        var live = theme.LiveReleaseId is { } liveReleaseId
            ? await ReleaseRepository.GetManifestAsync(liveReleaseId)
            : new List<SiteReleaseFile>();

        var filesByPath = files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        var liveByPath = live.ToDictionary(file => file.Path, file => file.ContentHash, StringComparer.Ordinal);

        var items = SiteThemeChanges
            .Compare(
                files.Select(file => (file.Path, file.ContentHash)),
                live.Select(file => (file.Path, file.ContentHash)))
            .Select(change =>
            {
                filesByPath.TryGetValue(change.Path, out var file);
                return new SiteThemeFileSummaryDto
                {
                    Path = change.Path,
                    IsAsset = SiteThemePaths.IsAsset(change.Path),
                    ContentHash = file?.ContentHash ?? liveByPath[change.Path],
                    Status = change.Status,
                    LastModificationTime = file == null ? null : file.LastModificationTime ?? file.CreationTime
                };
            })
            .ToList();

        return new ListResultDto<SiteThemeFileSummaryDto>(items);
    }

    public virtual async Task<SiteThemeFileDto> GetFileAsync(string path)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        return MapToDto(await ThemeManager.GetFileAsync(theme, path));
    }

    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task<SiteThemeFileDto> SaveFileAsync(SaveSiteThemeFileDto input)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        var file = await ThemeManager.SaveFileAsync(theme, input.Path, input.Body, input.ConcurrencyStamp);

        return MapToDto(file);
    }

    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task<SiteThemeRenameResultDto> RenameFileAsync(RenameSiteThemeFileDto input)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        var oldPath = SiteThemePaths.Normalize(input.Path);
        var rename = await ThemeManager.RenameFileAsync(theme, oldPath, input.NewPath, input.UpdateReferences);

        var pages = await ThemeManager.GetPageTemplatesAsync();

        return new SiteThemeRenameResultDto
        {
            File = MapToDto(rename.File),
            UpdatedPaths = rename.UpdatedPaths.ToList(),
            DefaultLayoutUpdated = rename.DefaultLayoutUpdated,
            PagesStillUsingOldPath = pages
                .Where(page => SiteThemePaths.Normalize(page.Template) == oldPath)
                .Select(page => page.PageName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList()
        };
    }

    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task DeleteFileAsync(string path)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        await ThemeManager.DeleteFileAsync(theme, path);
    }

    public virtual async Task<SiteThemeFileUsagesDto> GetFileUsagesAsync(string path)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        var file = await ThemeManager.GetFileAsync(theme, path);
        var check = await ThemeManager.CheckAsync(theme);

        return new SiteThemeFileUsagesDto
        {
            Path = file.Path,
            Usages = check.Analysis.GetUsages(file.Path)
                .Select(usage => new SiteThemeFileUsageDto
                {
                    Kind = (SiteThemeUsageKindDto)usage.Kind,
                    SourcePath = usage.SourcePath,
                    PageName = usage.PageName
                })
                .ToList()
        };
    }

    public virtual async Task<SiteThemeCheckDto> CheckAsync()
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        var check = await ThemeManager.CheckAsync(theme);

        // A theme that does not read cleanly renders into errors that are only its errors again, so the pages are
        // rendered once the reading finds nothing wrong.
        var issues = check.Analysis.Issues.ToList();
        if (!check.Analysis.HasErrors)
        {
            issues.AddRange(await TrialRenderAsync(theme, await FileRepository.GetListAsync(theme.Id), check));
        }

        return new SiteThemeCheckDto
        {
            Fingerprint = check.Fingerprint,
            // Errors first; the sort is stable, so the findings keep their order within a severity.
            Issues = issues.OrderBy(issue => issue.Severity).Select(MapToDto).ToList(),
            HasErrors = issues.Any(issue => issue.Severity == SiteThemeIssueSeverity.Error),
            Layouts = check.Analysis.Layouts.ToList(),
            Changes = check.Changes
                .Where(change => change.Status != SiteThemeFileStatus.Unchanged)
                .Select(change => new SiteThemeFileChangeDto { Path = change.Path, Status = change.Status })
                .ToList(),
            DefaultLayout = check.DefaultLayout,
            LiveDefaultLayout = check.LiveRelease?.DefaultLayout,
            LiveReleaseNumber = check.LiveRelease?.Number,
            HasChanges = check.HasChanges
        };
    }

    /// <summary>
    /// The domain agrees to the working copy as it was checked - the fingerprint, the errors the reading finds, something to
    /// publish - and then the pages are rendered with it, which finds the errors only a render finds and which the domain
    /// cannot do: the renderer is the rendering library's, not the domain's. Nothing is checked twice, and no page is rendered
    /// for a publish the domain would refuse.
    /// </summary>
    [Authorize(SiteAdminPermissions.Themes.Publish)]
    public virtual async Task<SiteReleaseDto> PublishAsync(PublishSiteThemeDto input)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        var publication = await ThemeManager.PrepareAsync(theme, input.Fingerprint);

        var failures = (await TrialRenderAsync(theme, publication.Files, publication.Check))
            .Count(issue => issue.Severity == SiteThemeIssueSeverity.Error);
        if (failures > 0)
        {
            throw new BusinessException(SiteErrorCodes.SiteThemeHasErrors).WithData("Count", failures);
        }

        var release = await ThemeManager.PublishAsync(publication, input.Comment);

        return MapToDto(release, theme);
    }

    protected virtual async Task<List<SiteThemeIssue>> TrialRenderAsync(
        SiteTheme theme,
        IEnumerable<SiteThemeFile> files,
        SiteThemeCheck check)
    {
        return await TrialRenderer.RenderAsync(
            ToWorkingCopySnapshot(theme, files),
            check.Analysis,
            RequestCancellationToken);
    }

    public virtual async Task<ListResultDto<SiteReleaseDto>> GetReleasesAsync()
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        var releases = await ReleaseRepository.GetListAsync(theme.Id);

        return new ListResultDto<SiteReleaseDto>(releases.Select(release => MapToDto(release, theme)).ToList());
    }

    public virtual async Task<SiteReleaseDetailDto> GetReleaseAsync(Guid id)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        var release = await ThemeManager.GetReleaseAsync(theme, id);
        var files = await ReleaseRepository.GetManifestAsync(release.Id);

        var dto = new SiteReleaseDetailDto
        {
            Files = files
                .Select(file => new SiteReleaseFileDto
                {
                    Path = file.Path,
                    IsAsset = SiteThemePaths.IsAsset(file.Path),
                    ContentHash = file.ContentHash
                })
                .ToList()
        };

        Fill(dto, release, theme);
        return dto;
    }

    public virtual async Task<SiteReleaseFileContentDto> GetReleaseFileAsync(Guid id, string path)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        var release = await ThemeManager.GetReleaseAsync(theme, id);
        var normalized = SiteThemePaths.Normalize(path);

        var content = await ReleaseRepository.FindContentAsync(release.Id, normalized)
                      ?? throw new EntityNotFoundException(typeof(SiteReleaseFile), normalized);

        return new SiteReleaseFileContentDto
        {
            Path = content.Path,
            IsAsset = SiteThemePaths.IsAsset(content.Path),
            ContentHash = content.ContentHash,
            Body = content.Body ?? string.Empty
        };
    }

    [Authorize(SiteAdminPermissions.Themes.Publish)]
    public virtual async Task<SiteReleaseDto> SetLiveReleaseAsync(Guid id)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        var release = await ThemeManager.SetLiveReleaseAsync(theme, id);

        return MapToDto(release, theme);
    }

    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task RestoreReleaseAsync(Guid id)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        await ThemeManager.RestoreReleaseAsync(theme, id);
    }

    public virtual async Task<SiteThemePreviewDto> PreviewAsync(string path, Guid? releaseId = null)
    {
        var theme = await ThemeManager.GetOrCreateAsync();

        // Resolved lazily and optionally: the renderer is Dignite.Site.Public.Web's, which this layer cannot
        // reference, and an administration API hosted without it simply cannot preview.
        var renderer = LazyServiceProvider.LazyGetService<ISiteThemePreviewRenderer>()
                       ?? throw new BusinessException(SiteErrorCodes.SiteTemplatePreviewUnavailable);

        var snapshot = releaseId is { } id
            ? await GetReleaseSnapshotAsync(theme, id)
            : await GetWorkingCopySnapshotAsync(theme);

        return await renderer.RenderAsync(
            new SiteThemePreviewRequest { Path = path, Snapshot = snapshot },
            RequestCancellationToken);
    }

    public virtual async Task<SiteThemePreviewLinkDto> GetPreviewLinkAsync(string? path = null, Guid? releaseId = null)
    {
        var theme = await ThemeManager.GetOrCreateAsync();
        if (releaseId is { } id)
        {
            // Refused here, while the author can still be told; the site would quietly show the live release.
            await ThemeManager.GetReleaseAsync(theme, id);
        }

        // Optional, like the preview renderer: the issuer is Dignite.Site.Public.Web's.
        var issuer = LazyServiceProvider.LazyGetService<ISiteThemePreviewLinkIssuer>()
                     ?? throw new BusinessException(SiteErrorCodes.SiteTemplatePreviewUnavailable);

        return await issuer.IssueAsync(new SiteThemePreviewLinkRequest { Path = path, ReleaseId = releaseId });
    }

    protected virtual async Task<SiteThemeSnapshotDto> GetWorkingCopySnapshotAsync(SiteTheme theme)
    {
        return ToWorkingCopySnapshot(theme, await FileRepository.GetListAsync(theme.Id));
    }

    protected virtual SiteThemeSnapshotDto ToWorkingCopySnapshot(SiteTheme theme, IEnumerable<SiteThemeFile> files)
    {
        return new SiteThemeSnapshotDto
        {
            DefaultLayout = theme.DefaultLayout,
            Files = files
                .Select(file => new SiteThemeSnapshotFileDto { Path = file.Path, ContentHash = file.ContentHash, Body = file.Body })
                .ToList()
        };
    }

    protected virtual async Task<SiteThemeSnapshotDto> GetReleaseSnapshotAsync(SiteTheme theme, Guid releaseId)
    {
        var release = await ThemeManager.GetReleaseAsync(theme, releaseId);
        var contents = await ReleaseRepository.GetContentsAsync(release.Id);

        return new SiteThemeSnapshotDto
        {
            ReleaseId = release.Id,
            ReleaseNumber = release.Number,
            DefaultLayout = release.DefaultLayout,
            Files = contents
                .Select(content => new SiteThemeSnapshotFileDto
                {
                    Path = content.Path,
                    ContentHash = content.ContentHash,
                    Body = content.Body ?? string.Empty
                })
                .ToList()
        };
    }

    protected virtual async Task<SiteThemeDto> MapToDtoAsync(SiteTheme theme)
    {
        int? liveReleaseNumber = null;
        if (theme.LiveReleaseId is { } liveReleaseId)
        {
            liveReleaseNumber = (await ReleaseRepository.GetAsync(liveReleaseId, includeDetails: false)).Number;
        }

        return new SiteThemeDto
        {
            DefaultLayout = theme.DefaultLayout,
            LiveReleaseId = theme.LiveReleaseId,
            LiveReleaseNumber = liveReleaseNumber,
            ConcurrencyStamp = theme.ConcurrencyStamp
        };
    }

    protected virtual SiteThemeFileDto MapToDto(SiteThemeFile file)
    {
        return new SiteThemeFileDto
        {
            Path = file.Path,
            IsAsset = file.IsAsset,
            Body = file.Body,
            ContentHash = file.ContentHash,
            ConcurrencyStamp = file.ConcurrencyStamp,
            LastModificationTime = file.LastModificationTime ?? file.CreationTime,
            SyntaxErrors = file.IsAsset
                ? new List<SiteThemeSyntaxErrorDto>()
                : ScribanTemplateSyntax.GetErrors(file.Body)
                    .Select(error => new SiteThemeSyntaxErrorDto { Line = error.Line, Column = error.Column, Message = error.Message })
                    .ToList()
        };
    }

    protected virtual SiteReleaseDto MapToDto(SiteRelease release, SiteTheme theme)
    {
        var dto = new SiteReleaseDto();
        Fill(dto, release, theme);
        return dto;
    }

    protected virtual void Fill(SiteReleaseDto dto, SiteRelease release, SiteTheme theme)
    {
        dto.Id = release.Id;
        dto.Number = release.Number;
        dto.Comment = release.Comment;
        dto.DefaultLayout = release.DefaultLayout;
        dto.ChangedFileCount = release.ChangedFileCount;
        dto.CreationTime = release.CreationTime;
        dto.CreatorId = release.CreatorId;
        dto.IsLive = theme.LiveReleaseId == release.Id;
    }

    /// <summary>
    /// The finding with its message in the user's language: the code is the localization key, and its
    /// <c>{Placeholders}</c> are filled from the finding's data, the way a business exception's are.
    /// </summary>
    protected virtual SiteThemeIssueDto MapToDto(SiteThemeIssue issue)
    {
        var message = L[issue.Code].Value;
        foreach (var (key, value) in issue.Data)
        {
            message = message.Replace("{" + key + "}", Localize(key, value), StringComparison.Ordinal);
        }

        return new SiteThemeIssueDto
        {
            Severity = issue.Severity,
            Code = issue.Code,
            Path = issue.Path,
            Line = issue.Line,
            Column = issue.Column,
            Message = message
        };
    }

    /// <summary>A finding's data as its message shows it: the roles of a file are words, everything else is shown as it is.</summary>
    protected virtual string Localize(string key, string value)
    {
        if (key != "Roles")
        {
            return value;
        }

        return string.Join(
            L["ThemeRole:Separator"].Value,
            value.Split(", ", StringSplitOptions.RemoveEmptyEntries).Select(role => L["ThemeRole:" + role].Value));
    }
}
