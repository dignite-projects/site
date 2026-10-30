using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.Templates;

public class EfCoreSiteTemplateRepository
    : EfCoreRepository<ISiteDbContext, SiteTemplate, Guid>, ISiteTemplateRepository
{
    public EfCoreSiteTemplateRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<SiteTemplate?> FindByNameAsync(
        string name,
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .IncludeDetails(includeDetails)
            .FirstOrDefaultAsync(t => t.Name == name, GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> NameExistsAsync(
        string name,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(
                t => t.Name == name && (excludedId == null || t.Id != excludedId),
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<SiteTemplate>> GetListAsync(
        string? filter = null,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        var queryable = (await GetQueryableAsync()).IncludeDetails(includeDetails);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            // Lowered on both sides: SQLite translates Contains to a case-sensitive instr(), SQL Server's
            // default collation is case-insensitive already - this makes both answer the same.
            var lowered = filter.Trim().ToLower();
            queryable = queryable.Where(t => t.Name.ToLower().Contains(lowered));
        }

        return await queryable
            .OrderBy(t => t.Name)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<SiteTemplateVersion?> FindPublishedVersionAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var templates = await GetQueryableAsync();
        var versions = (await GetDbContextAsync()).SiteTemplateVersions;

        // A join on the template's pointer, so only the one live version is read - never the template's
        // whole history. The tenant and soft-delete filters apply to both sides.
        return await (
                from template in templates
                join version in versions on template.Id equals version.TemplateId
                where template.Name == name && template.PublishedVersionNumber == version.Number
                select version)
            .AsNoTracking()
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<Dictionary<Guid, int>> GetLatestVersionNumbersAsync(
        IEnumerable<Guid> templateIds,
        CancellationToken cancellationToken = default)
    {
        var ids = templateIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var rows = await (await GetDbContextAsync()).SiteTemplateVersions
            .Where(v => ids.Contains(v.TemplateId))
            .GroupBy(v => v.TemplateId)
            .Select(g => new { TemplateId = g.Key, Latest = g.Max(v => v.Number) })
            .ToListAsync(GetCancellationToken(cancellationToken));

        return rows.ToDictionary(r => r.TemplateId, r => r.Latest);
    }

    public override async Task<IQueryable<SiteTemplate>> WithDetailsAsync()
    {
        return (await GetQueryableAsync()).IncludeDetails();
    }
}

public static class SiteTemplateRepositoryQueryableExtensions
{
    public static IQueryable<SiteTemplate> IncludeDetails(this IQueryable<SiteTemplate> queryable, bool include = true)
    {
        return include ? queryable.Include(t => t.Versions) : queryable;
    }
}
