using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.Themes;

public class EfCoreSiteThemeRepository
    : EfCoreRepository<ISiteDbContext, SiteTheme, Guid>, ISiteThemeRepository
{
    public EfCoreSiteThemeRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<SiteTheme?> FindCurrentAsync(CancellationToken cancellationToken = default)
    {
        // The tenant filter narrows it to the current tenant's one theme.
        return await (await GetQueryableAsync())
            .OrderBy(theme => theme.CreationTime)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<Guid?> FindLiveReleaseIdAsync(CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .AsNoTracking()
            .OrderBy(theme => theme.CreationTime)
            .Select(theme => theme.LiveReleaseId)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }
}

public class EfCoreSiteThemeFileRepository
    : EfCoreRepository<ISiteDbContext, SiteThemeFile, Guid>, ISiteThemeFileRepository
{
    public EfCoreSiteThemeFileRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<SiteThemeFile?> FindByPathAsync(Guid themeId, string path, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .FirstOrDefaultAsync(file => file.ThemeId == themeId && file.Path == path, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> PathExistsAsync(Guid themeId, string path, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .AnyAsync(file => file.ThemeId == themeId && file.Path == path, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<SiteThemeFile>> GetListAsync(Guid themeId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Where(file => file.ThemeId == themeId)
            .OrderBy(file => file.Path)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<SiteThemeFileSummary>> GetSummariesAsync(Guid themeId, CancellationToken cancellationToken = default)
    {
        // Projected, so the bodies stay in the database.
        var rows = await (await GetQueryableAsync())
            .AsNoTracking()
            .Where(file => file.ThemeId == themeId)
            .OrderBy(file => file.Path)
            .Select(file => new { file.Id, file.Path, file.ContentHash, file.CreationTime, file.LastModificationTime })
            .ToListAsync(GetCancellationToken(cancellationToken));

        return rows
            .Select(row => new SiteThemeFileSummary(row.Id, row.Path, row.ContentHash, row.CreationTime, row.LastModificationTime))
            .ToList();
    }
}

public class EfCoreSiteReleaseRepository
    : EfCoreRepository<ISiteDbContext, SiteRelease, Guid>, ISiteReleaseRepository
{
    public EfCoreSiteReleaseRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<List<SiteRelease>> GetListAsync(Guid themeId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Where(release => release.ThemeId == themeId)
            .OrderByDescending(release => release.Number)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<int> GetMaxNumberAsync(Guid themeId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Where(release => release.ThemeId == themeId)
            .Select(release => (int?)release.Number)
            .MaxAsync(GetCancellationToken(cancellationToken)) ?? 0;
    }

    public virtual async Task<List<SiteReleaseFile>> GetManifestAsync(Guid releaseId, CancellationToken cancellationToken = default)
    {
        return await (await GetDbContextAsync()).SiteReleaseFiles
            .AsNoTracking()
            .Where(file => file.ReleaseId == releaseId)
            .OrderBy(file => file.Path)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<SiteReleaseFileContent>> GetContentsAsync(
        Guid releaseId,
        bool includeAssetBodies = false,
        CancellationToken cancellationToken = default)
    {
        var rows = await QueryContents(await GetDbContextAsync(), releaseId)
            .ToListAsync(GetCancellationToken(cancellationToken));

        // A static file's body is cut here rather than in SQL: telling a path with an extension from one without
        // is not something every provider translates, and a theme's static files are few and small next to a
        // page render's other reads. The theme's texts are the static files a render does read.
        return rows
            .Select(row => new SiteReleaseFileContent(
                row.Path,
                row.ContentHash,
                includeAssetBodies || SiteThemePaths.IsTemplate(row.Path) || SiteThemeTextFiles.IsTextFile(row.Path) ? row.Body : null))
            .OrderBy(row => row.Path, StringComparer.Ordinal)
            .ToList();
    }

    public virtual async Task<SiteReleaseFileContent?> FindContentAsync(Guid releaseId, string path, CancellationToken cancellationToken = default)
    {
        var row = await QueryContents(await GetDbContextAsync(), releaseId)
            .Where(content => content.Path == path)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));

        return row == null ? null : new SiteReleaseFileContent(row.Path, row.ContentHash, row.Body);
    }

    /// <summary>
    /// The release's files joined to their bodies. The blob side is filtered by tenant like any multi-tenant entity,
    /// so a release only ever reads its own tenant's bodies.
    /// </summary>
    protected virtual IQueryable<ReleaseFileRow> QueryContents(ISiteDbContext dbContext, Guid releaseId)
    {
        return from file in dbContext.SiteReleaseFiles.AsNoTracking()
               join blob in dbContext.SiteThemeBlobs.AsNoTracking() on file.ContentHash equals blob.ContentHash
               where file.ReleaseId == releaseId
               select new ReleaseFileRow { Path = file.Path, ContentHash = file.ContentHash, Body = blob.Body };
    }

    public override async Task<IQueryable<SiteRelease>> WithDetailsAsync()
    {
        return (await GetQueryableAsync()).Include(release => release.Files);
    }

    protected class ReleaseFileRow
    {
        public string Path { get; set; } = default!;
        public string ContentHash { get; set; } = default!;
        public string Body { get; set; } = default!;
    }
}

public class EfCoreSiteThemeBlobRepository
    : EfCoreRepository<ISiteDbContext, SiteThemeBlob, Guid>, ISiteThemeBlobRepository
{
    public EfCoreSiteThemeBlobRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<HashSet<string>> GetExistingHashesAsync(IEnumerable<string> contentHashes, CancellationToken cancellationToken = default)
    {
        var hashes = contentHashes.Distinct(StringComparer.Ordinal).ToList();
        if (hashes.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var existing = await (await GetQueryableAsync())
            .Where(blob => hashes.Contains(blob.ContentHash))
            .Select(blob => blob.ContentHash)
            .ToListAsync(GetCancellationToken(cancellationToken));

        return new HashSet<string>(existing, StringComparer.Ordinal);
    }
}
