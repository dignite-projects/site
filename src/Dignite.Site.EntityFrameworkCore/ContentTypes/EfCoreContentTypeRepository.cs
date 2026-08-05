using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.ContentTypes;

public class EfCoreContentTypeRepository : EfCoreRepository<ISiteDbContext, ContentType, Guid>, IContentTypeRepository
{
    public EfCoreContentTypeRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<List<ContentType>> GetListByPageAsync(
        Guid pageId,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(ct => ct.PageId == pageId)
            .OrderBy(ct => ct.Name)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<ContentType?> FindByNameAsync(
        Guid pageId,
        string name,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .FirstOrDefaultAsync(
                ct => ct.PageId == pageId && ct.Name == name,
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> NameExistsAsync(
        Guid pageId,
        string name,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(
                ct => ct.PageId == pageId && ct.Name == name && (excludedId == null || ct.Id != excludedId),
                GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<ContentType>> GetListAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .Where(ct => ids.Contains(ct.Id))
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    /// <summary>
    /// Which content types pull in a given field.
    /// <para>
    /// Evaluated in memory, deliberately and with a bounded cost: the field arrangement is a JSON
    /// column, so there is no queryable column to filter on, and the alternative - a join table - would
    /// exist solely for this call. The set being scanned is content types, of which a site has tens, not
    /// contents, of which it has thousands. This is not the in-memory filtering §2.4 rules out; that one
    /// was over the content table on every list query.
    /// </para>
    /// </summary>
    public virtual async Task<List<ContentType>> GetListByFieldAsync(
        Guid fieldId,
        CancellationToken cancellationToken = default)
    {
        var all = await (await GetDbSetAsync()).ToListAsync(GetCancellationToken(cancellationToken));

        return all.Where(ct => ct.HasField(fieldId)).ToList();
    }
}
