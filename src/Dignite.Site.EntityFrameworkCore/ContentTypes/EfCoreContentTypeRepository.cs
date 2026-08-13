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
}
