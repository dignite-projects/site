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
        string? prefix = null,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        var queryable = (await GetQueryableAsync()).IncludeDetails(includeDetails);

        if (!string.IsNullOrWhiteSpace(prefix))
        {
            // StartsWith is a LIKE 'prefix%' - an ordinal match on the stored (already normalized) name.
            var normalizedPrefix = SiteTemplateNames.Normalize(prefix);
            queryable = queryable.Where(t => t.Name.StartsWith(normalizedPrefix));
        }

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

    public virtual async Task<List<PublishedSiteTemplateVersion>> GetPublishedRenderSetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var templates = await GetQueryableAsync();
        var versions = (await GetDbContextAsync()).SiteTemplateVersions;

        // One query for the whole render set instead of one per template or include: a page view needs the
        // page template, the layout, the partials it may include and the asset versions it may link, and
        // the set a render sees is then fixed when it starts. Same join on the template's pointer as
        // FindPublishedVersionAsync, so only live versions are read - never a template's history.
        // An asset's body is never selected (null in SQL, not read): the renderer only needs its version
        // number, and a stylesheet or script can be large and sits in every page view's path otherwise.
        var rows = await (
                from template in templates
                join version in versions on template.Id equals version.TemplateId
                where template.PublishedVersionNumber == version.Number
                      && (template.Name == name
                          || template.Name == SiteTemplateConsts.LayoutName
                          || template.Name.StartsWith(SiteTemplateConsts.PartialsPrefix)
                          || template.Name.StartsWith(SiteTemplateConsts.AssetsPrefix))
                select new
                {
                    TemplateName = template.Name,
                    version.Id,
                    version.TemplateId,
                    version.Number,
                    Body = template.Name.StartsWith(SiteTemplateConsts.AssetsPrefix) ? null : version.Body,
                    version.Comment,
                    version.CreationTime
                })
            .AsNoTracking()
            .ToListAsync(GetCancellationToken(cancellationToken));

        return rows
            .Select(r => new PublishedSiteTemplateVersion(
                r.TemplateName, r.Id, r.TemplateId, r.Number, r.Body, r.Comment, r.CreationTime))
            .ToList();
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
