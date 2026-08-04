using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Dignite.Sites.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Sites.Contents;

/// <summary>
/// Pushes a field filter down into SQL: each condition becomes a subquery over
/// <see cref="ContentFlexFieldIndex"/>, compared against the typed column its value type names, and
/// composed onto the content query so the whole thing resolves in one round trip.
/// <para>
/// This is the fix for the specific weakness Sites inherited the model from. Dignite.Cms stored field
/// values in an <c>ExtraProperties</c> JSON column with no typed projection, so
/// <c>EfCoreEntryRepository.GetListAsync</c> fell back to <c>AsEnumerable()</c> the moment a custom-field
/// condition was present and filtered the entire table in memory (总体设计 §2.4).
/// </para>
/// <para>
/// Resolves as <c>IFlexFieldQueryExecutor&lt;Content&gt;</c> by ABP's naming convention.
/// </para>
/// </summary>
public class ContentFlexFieldQueryExecutor
    : EfCoreFlexFieldQueryExecutorBase<ISitesDbContext, Content, ContentFlexFieldIndex>,
      ITransientDependency
{
    protected override string EntityIdPropertyName => nameof(ContentFlexFieldIndex.ContentId);

    public ContentFlexFieldQueryExecutor(IDbContextProvider<ISitesDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
