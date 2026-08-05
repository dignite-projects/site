using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.Fields;

public class EfCoreFieldGroupRepository : EfCoreRepository<ISiteDbContext, FieldGroup, Guid>, IFieldGroupRepository
{
    public EfCoreFieldGroupRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<List<FieldGroup>> GetListAsync(
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .IncludeDetails(includeDetails)
            .OrderBy(fg => fg.Order)
            .ThenBy(fg => fg.Name)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> NameExistsAsync(
        string name,
        Guid? excludedId = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .AnyAsync(
                fg => fg.Name == name && (excludedId == null || fg.Id != excludedId),
                GetCancellationToken(cancellationToken));
    }

    public override async Task<IQueryable<FieldGroup>> WithDetailsAsync()
    {
        return (await GetQueryableAsync()).IncludeDetails();
    }
}

public static class FieldGroupRepositoryQueryableExtensions
{
    public static IQueryable<FieldGroup> IncludeDetails(this IQueryable<FieldGroup> queryable, bool include = true)
    {
        return include ? queryable.Include(fg => fg.Fields) : queryable;
    }
}
