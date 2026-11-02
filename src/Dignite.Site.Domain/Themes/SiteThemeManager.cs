using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Themes;

/// <summary>What <see cref="SiteThemeManager.CheckAsync"/> found: the analysis, and how the working copy differs from the live release.</summary>
public sealed class SiteThemeCheck
{
    public SiteThemeCheck(
        SiteThemeAnalysis analysis,
        string fingerprint,
        IReadOnlyList<SiteThemeChange> changes,
        string? defaultLayout,
        SiteRelease? liveRelease)
    {
        Analysis = analysis;
        Fingerprint = fingerprint;
        Changes = changes;
        DefaultLayout = defaultLayout;
        LiveRelease = liveRelease;
    }

    public SiteThemeAnalysis Analysis { get; }

    /// <summary>The working copy's <see cref="SiteThemeFingerprint"/>, for the publish to bring back.</summary>
    public string Fingerprint { get; }

    /// <summary>Every path of the working copy and the live release, with its status.</summary>
    public IReadOnlyList<SiteThemeChange> Changes { get; }

    /// <summary>The working copy's default layout setting.</summary>
    public string? DefaultLayout { get; }

    /// <summary>The live release, without its files, or null when nothing was published.</summary>
    public SiteRelease? LiveRelease { get; }

    /// <summary>Whether publishing would change anything: a file, or the default layout setting.</summary>
    public bool HasChanges => LiveRelease == null
        ? Changes.Count > 0 || DefaultLayout != null
        : Changes.Any(change => change.Status != SiteThemeFileStatus.Unchanged)
          || !string.Equals(DefaultLayout, LiveRelease.DefaultLayout, StringComparison.Ordinal);
}

/// <summary>The outcome of <see cref="SiteThemeManager.RenameFileAsync"/>.</summary>
public sealed record SiteThemeRename(SiteThemeFile File, IReadOnlyList<string> UpdatedPaths, bool DefaultLayoutUpdated);

/// <summary>
/// Everything that changes a theme: the working copy's files and setting, publishing it as a release, and moving
/// the live pointer back and forth. See <see cref="SiteTheme"/> for the model.
/// </summary>
public class SiteThemeManager : DomainService
{
    protected ISiteThemeRepository ThemeRepository { get; }
    protected ISiteThemeFileRepository FileRepository { get; }
    protected ISiteReleaseRepository ReleaseRepository { get; }
    protected ISiteThemeBlobRepository BlobRepository { get; }
    protected IPageRepository PageRepository { get; }

    public SiteThemeManager(
        ISiteThemeRepository themeRepository,
        ISiteThemeFileRepository fileRepository,
        ISiteReleaseRepository releaseRepository,
        ISiteThemeBlobRepository blobRepository,
        IPageRepository pageRepository)
    {
        ThemeRepository = themeRepository;
        FileRepository = fileRepository;
        ReleaseRepository = releaseRepository;
        BlobRepository = blobRepository;
        PageRepository = pageRepository;
    }

    /// <summary>
    /// The current tenant's theme, created on first use with the skeleton files (<see cref="SiteThemeSkeleton"/>)
    /// and <see cref="SiteThemeConsts.InitialDefaultLayout"/> as its default layout. Creating it publishes nothing:
    /// the site keeps rendering what it rendered until a first release.
    /// </summary>
    public virtual async Task<SiteTheme> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var theme = await ThemeRepository.FindCurrentAsync(cancellationToken);
        if (theme != null)
        {
            return theme;
        }

        theme = new SiteTheme(GuidGenerator.Create(), CurrentTenant.Id);
        await ThemeRepository.InsertAsync(theme, autoSave: true, cancellationToken: cancellationToken);

        // Saved at once, like the theme: the caller usually goes on to query the files in the same unit of work.
        await FileRepository.InsertManyAsync(
            SiteThemeSkeleton.Files.Select(file => new SiteThemeFile(GuidGenerator.Create(), theme.Id, file.Path, file.Body, CurrentTenant.Id)),
            autoSave: true,
            cancellationToken: cancellationToken);

        return theme;
    }

    /// <summary>
    /// Writes <paramref name="body"/> to the file at <paramref name="path"/>, creating it when there is none. With a
    /// <paramref name="concurrencyStamp"/>, an existing file must still have it, or the save fails as a concurrency
    /// conflict; without one, an existing file is overwritten. A body that does not parse is saved all the same.
    /// </summary>
    public virtual async Task<SiteThemeFile> SaveFileAsync(
        SiteTheme theme,
        string path,
        string body,
        string? concurrencyStamp = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = SiteThemePaths.Normalize(path);
        var file = await FileRepository.FindByPathAsync(theme.Id, normalized, cancellationToken);

        if (file == null)
        {
            file = new SiteThemeFile(GuidGenerator.Create(), theme.Id, normalized, body, CurrentTenant.Id);
            return await FileRepository.InsertAsync(file, autoSave: true, cancellationToken: cancellationToken);
        }

        if (concurrencyStamp != null)
        {
            file.ConcurrencyStamp = concurrencyStamp;
        }

        file.SetBody(body);
        return await FileRepository.UpdateAsync(file, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Moves a file to <paramref name="newPath"/>. With <paramref name="updateReferences"/>, every literal
    /// <c>include</c>, <c>asset_url</c> and front matter <c>layout</c> naming the old path is rewritten to the new
    /// one, and so is the default layout setting. Pages whose <c>Template</c> names the old path are not changed.
    /// </summary>
    public virtual async Task<SiteThemeRename> RenameFileAsync(
        SiteTheme theme,
        string path,
        string newPath,
        bool updateReferences,
        CancellationToken cancellationToken = default)
    {
        var oldPath = SiteThemePaths.Normalize(path);
        var targetPath = SiteThemePaths.Normalize(newPath);

        var file = await GetFileAsync(theme, oldPath, cancellationToken);
        if (targetPath == oldPath)
        {
            return new SiteThemeRename(file, Array.Empty<string>(), false);
        }

        if (await FileRepository.PathExistsAsync(theme.Id, targetPath, cancellationToken))
        {
            throw new BusinessException(SiteErrorCodes.SiteThemeFileAlreadyExists).WithData("Path", targetPath);
        }

        file.SetPath(targetPath);

        var updatedPaths = new List<string>();
        var defaultLayoutUpdated = false;

        if (updateReferences)
        {
            var files = await FileRepository.GetListAsync(theme.Id, cancellationToken);
            foreach (var source in files.Where(f => !f.IsAsset))
            {
                var template = ScribanTemplateSyntax.Parse(source.Body, source.Path);
                if (template.HasErrors)
                {
                    continue;
                }

                var references = ScribanTemplateSyntax.GetReferences(template)
                    .Append(ScribanTemplateSyntax.ReadFrontMatter(template).Layout)
                    .Where(reference => reference != null && reference.Path == oldPath)
                    .Select(reference => reference!)
                    .ToList();

                if (references.Count == 0)
                {
                    continue;
                }

                // The renamed file is one of these when it names itself; it is tracked already and saved below.
                var target = source.Id == file.Id ? file : source;
                target.SetBody(SiteThemeReferenceRewriter.Rewrite(source.Body, references, targetPath));
                updatedPaths.Add(target.Path);

                if (target != file)
                {
                    await FileRepository.UpdateAsync(target, cancellationToken: cancellationToken);
                }
            }

            if (theme.DefaultLayout == oldPath)
            {
                theme.SetDefaultLayout(targetPath);
                await ThemeRepository.UpdateAsync(theme, cancellationToken: cancellationToken);
                defaultLayoutUpdated = true;
            }
        }

        await FileRepository.UpdateAsync(file, autoSave: true, cancellationToken: cancellationToken);

        return new SiteThemeRename(file, updatedPaths.OrderBy(p => p, StringComparer.Ordinal).ToList(), defaultLayoutUpdated);
    }

    public virtual async Task DeleteFileAsync(SiteTheme theme, string path, CancellationToken cancellationToken = default)
    {
        var file = await GetFileAsync(theme, SiteThemePaths.Normalize(path), cancellationToken);

        await FileRepository.DeleteAsync(file, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Sets the working copy's default layout; with a <paramref name="concurrencyStamp"/>, the theme must still have it.
    /// A layout is told by what it does, as the check tells it - a file has no type: it must be a template of the
    /// working copy (<see cref="SiteErrorCodes.SiteThemeDefaultLayoutNotFound"/>) that writes the page's <c>body</c>,
    /// calls <c>site_head</c> (itself or through what it includes) and has no front matter
    /// (<see cref="SiteErrorCodes.SiteThemeDefaultLayoutNotALayout"/>). A template that does not parse cannot be
    /// told, and is let through: the check reports its syntax errors.
    /// </summary>
    public virtual async Task SetDefaultLayoutAsync(
        SiteTheme theme,
        string? defaultLayout,
        string? concurrencyStamp = null,
        CancellationToken cancellationToken = default)
    {
        // Everything is judged before the theme is touched.
        var layout = SiteTheme.NormalizeDefaultLayout(defaultLayout);
        if (layout != null)
        {
            await EnsureIsLayoutAsync(theme, layout, cancellationToken);
        }

        if (concurrencyStamp != null)
        {
            theme.ConcurrencyStamp = concurrencyStamp;
        }

        theme.SetDefaultLayout(layout);
        await ThemeRepository.UpdateAsync(theme, autoSave: true, cancellationToken: cancellationToken);
    }

    protected virtual async Task EnsureIsLayoutAsync(SiteTheme theme, string layout, CancellationToken cancellationToken)
    {
        var files = await FileRepository.GetListAsync(theme.Id, cancellationToken);

        // The layouts do not depend on the pages.
        var analysis = SiteThemeAnalyzer.Analyze(
            files.Select(file => new SiteThemeSourceFile(file.Path, file.Body)),
            defaultLayout: null,
            Array.Empty<SiteThemePageTemplate>());

        if (!analysis.Files.TryGetValue(layout, out var layoutFile) || layoutFile.IsAsset)
        {
            throw new BusinessException(SiteErrorCodes.SiteThemeDefaultLayoutNotFound).WithData("Layout", layout);
        }

        // The same list the check gives the admin UI to offer "set as default layout" on.
        if (layoutFile.SyntaxErrors.Count == 0 && !analysis.Layouts.Contains(layout))
        {
            throw new BusinessException(SiteErrorCodes.SiteThemeDefaultLayoutNotALayout).WithData("Layout", layout);
        }
    }

    /// <summary>Checks the whole working copy and compares it with the live release - see <see cref="SiteThemeCheck"/>.</summary>
    public virtual async Task<SiteThemeCheck> CheckAsync(SiteTheme theme, CancellationToken cancellationToken = default)
    {
        var files = await FileRepository.GetListAsync(theme.Id, cancellationToken);
        return await CheckAsync(theme, files, cancellationToken);
    }

    /// <summary>
    /// Publishes the working copy as a new release and makes it live. Refused when <paramref name="fingerprint"/> is
    /// not the working copy's any more (<see cref="SiteErrorCodes.SiteThemeChangedSinceCheck"/>), when the check finds
    /// an error (<see cref="SiteErrorCodes.SiteThemeHasErrors"/>), and when nothing changed since the live release
    /// (<see cref="SiteErrorCodes.SiteThemeNoChanges"/>).
    /// </summary>
    public virtual async Task<SiteRelease> PublishAsync(
        SiteTheme theme,
        string? comment,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        var files = await FileRepository.GetListAsync(theme.Id, cancellationToken);
        var check = await CheckAsync(theme, files, cancellationToken);

        if (!string.Equals(check.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new BusinessException(SiteErrorCodes.SiteThemeChangedSinceCheck);
        }

        var errorCount = check.Analysis.Issues.Count(issue => issue.Severity == SiteThemeIssueSeverity.Error);
        if (errorCount > 0)
        {
            throw new BusinessException(SiteErrorCodes.SiteThemeHasErrors).WithData("Count", errorCount);
        }

        if (!check.HasChanges)
        {
            throw new BusinessException(SiteErrorCodes.SiteThemeNoChanges);
        }

        await StoreBlobsAsync(files, cancellationToken);

        var number = await ReleaseRepository.GetMaxNumberAsync(theme.Id, cancellationToken) + 1;
        var release = new SiteRelease(
            GuidGenerator.Create(),
            theme.Id,
            number,
            theme.DefaultLayout,
            comment,
            check.Changes.Count(change => change.Status != SiteThemeFileStatus.Unchanged),
            files.Select(file => (file.Path, file.ContentHash)),
            CurrentTenant.Id);

        await ReleaseRepository.InsertAsync(release, cancellationToken: cancellationToken);

        theme.SetLiveRelease(release.Id);
        await ThemeRepository.UpdateAsync(theme, autoSave: true, cancellationToken: cancellationToken);

        return release;
    }

    /// <summary>Makes an existing release the live one - rolling back, or forward again.</summary>
    public virtual async Task<SiteRelease> SetLiveReleaseAsync(SiteTheme theme, Guid releaseId, CancellationToken cancellationToken = default)
    {
        var release = await GetReleaseAsync(theme, releaseId, cancellationToken);

        theme.SetLiveRelease(release.Id);
        await ThemeRepository.UpdateAsync(theme, autoSave: true, cancellationToken: cancellationToken);

        return release;
    }

    /// <summary>
    /// Replaces the working copy with a release: its files, as they were, and its default layout. A file of the
    /// working copy that the release does not have is deleted. Nothing goes live.
    /// </summary>
    public virtual async Task RestoreReleaseAsync(SiteTheme theme, Guid releaseId, CancellationToken cancellationToken = default)
    {
        var release = await GetReleaseAsync(theme, releaseId, cancellationToken);
        var contents = await ReleaseRepository.GetContentsAsync(release.Id, includeAssetBodies: true, cancellationToken);
        var working = (await FileRepository.GetListAsync(theme.Id, cancellationToken))
            .ToDictionary(file => file.Path, StringComparer.Ordinal);

        foreach (var content in contents)
        {
            var body = content.Body ?? string.Empty;
            if (working.Remove(content.Path, out var existing))
            {
                if (existing.ContentHash != content.ContentHash)
                {
                    existing.SetBody(body);
                    await FileRepository.UpdateAsync(existing, cancellationToken: cancellationToken);
                }

                continue;
            }

            await FileRepository.InsertAsync(
                new SiteThemeFile(GuidGenerator.Create(), theme.Id, content.Path, body, CurrentTenant.Id),
                cancellationToken: cancellationToken);
        }

        await FileRepository.DeleteManyAsync(working.Values, cancellationToken: cancellationToken);

        theme.SetDefaultLayout(release.DefaultLayout);
        await ThemeRepository.UpdateAsync(theme, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>The routable and unroutable pages alike, as the check reads them: a page switched off today may be switched on tomorrow.</summary>
    public virtual async Task<List<SiteThemePageTemplate>> GetPageTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var pages = await PageRepository.GetListAsync(cancellationToken: cancellationToken);
        return pages.Select(page => new SiteThemePageTemplate(page.Name, page.Template)).ToList();
    }

    public virtual async Task<SiteThemeFile> GetFileAsync(SiteTheme theme, string path, CancellationToken cancellationToken = default)
    {
        return await FileRepository.FindByPathAsync(theme.Id, SiteThemePaths.Normalize(path), cancellationToken)
               ?? throw new EntityNotFoundException(typeof(SiteThemeFile), path);
    }

    public virtual async Task<SiteRelease> GetReleaseAsync(SiteTheme theme, Guid releaseId, CancellationToken cancellationToken = default)
    {
        var release = await ReleaseRepository.GetAsync(releaseId, includeDetails: false, cancellationToken);
        if (release.ThemeId != theme.Id)
        {
            throw new EntityNotFoundException(typeof(SiteRelease), releaseId);
        }

        return release;
    }

    protected virtual async Task<SiteThemeCheck> CheckAsync(SiteTheme theme, List<SiteThemeFile> files, CancellationToken cancellationToken)
    {
        var pages = await GetPageTemplatesAsync(cancellationToken);
        var analysis = SiteThemeAnalyzer.Analyze(
            files.Select(file => new SiteThemeSourceFile(file.Path, file.Body)),
            theme.DefaultLayout,
            pages);

        var working = files.Select(file => (file.Path, file.ContentHash)).ToList();

        SiteRelease? liveRelease = null;
        var live = new List<(string Path, string ContentHash)>();
        if (theme.LiveReleaseId is { } liveReleaseId)
        {
            liveRelease = await ReleaseRepository.GetAsync(liveReleaseId, includeDetails: false, cancellationToken);
            live = (await ReleaseRepository.GetManifestAsync(liveReleaseId, cancellationToken))
                .Select(file => (file.Path, file.ContentHash))
                .ToList();
        }

        return new SiteThemeCheck(
            analysis,
            SiteThemeFingerprint.Compute(working, theme.DefaultLayout),
            SiteThemeChanges.Compare(working, live),
            theme.DefaultLayout,
            liveRelease);
    }

    /// <summary>Stores a blob for every content the tenant has none for yet - once per content, however many files share it.</summary>
    protected virtual async Task StoreBlobsAsync(List<SiteThemeFile> files, CancellationToken cancellationToken)
    {
        var bodiesByHash = files
            .GroupBy(file => file.ContentHash, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Body, StringComparer.Ordinal);

        var existing = await BlobRepository.GetExistingHashesAsync(bodiesByHash.Keys, cancellationToken);

        var blobs = bodiesByHash
            .Where(pair => !existing.Contains(pair.Key))
            .Select(pair => new SiteThemeBlob(GuidGenerator.Create(), pair.Key, pair.Value, CurrentTenant.Id))
            .ToList();

        if (blobs.Count > 0)
        {
            await BlobRepository.InsertManyAsync(blobs, cancellationToken: cancellationToken);
        }
    }
}
