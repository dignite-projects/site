using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Site.Fields;

/// <summary>
/// The field library's repository.
/// <para>
/// Inherits the kernel's <c>EfCoreFlexFieldRepositoryBase</c>, which already implements the three
/// definition-axis lookups (<c>FindByNameAsync</c>, <c>GetListAsync(ids)</c>, <c>NameExistsAsync</c>) -
/// including the one the rename flow depends on.
/// </para>
/// <para>
/// The base self-registers as <c>ITransientDependency</c>, but that only reaches
/// <c>IFlexFieldRepository&lt;Field&gt;</c> for a class whose name ends with
/// "FlexFieldRepository" - this one does not, so <c>SiteEntityFrameworkCoreModule</c> binds it
/// explicitly. The base class documents that requirement.
/// </para>
/// </summary>
public class EfCoreFieldRepository : EfCoreFlexFieldRepositoryBase<ISiteDbContext, Field>, IFieldRepository
{
    public EfCoreFieldRepository(IDbContextProvider<ISiteDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<List<Field>> GetListAsync(
        Guid? groupId = null,
        string? filter = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        string? sorting = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetFilteredQueryableAsync(groupId, filter))
            .OrderBy(sorting.IsNullOrWhiteSpace() ? $"{nameof(Field.Name)} asc" : sorting!)
            .PageBy(skipCount, maxResultCount)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<int> GetCountAsync(
        Guid? groupId = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        return await (await GetFilteredQueryableAsync(groupId, filter))
            .CountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual async Task<IQueryable<Field>> GetFilteredQueryableAsync(Guid? groupId, string? filter)
    {
        return (await GetDbSetAsync())
            .WhereIf(groupId.HasValue, f => f.GroupId == groupId)
            .WhereIf(
                !filter.IsNullOrWhiteSpace(),
                f => f.Name.Contains(filter!) || f.DisplayName.Contains(filter!));
    }
}
