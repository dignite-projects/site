using System;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Dignite.Sites.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace Dignite.Sites.Contents;

/// <summary>
/// Keeps <see cref="ContentFlexFieldIndex"/> in step with the contents' value bags.
/// <para>
/// Everything about how that is done - paging, which values are eligible, decomposing a multi-valued
/// field into one row per value, typing each one into its slot - comes from the kernel. The two
/// overrides below are the only things it cannot know: the name of Sites' own foreign key, and how to
/// build one of Sites' own rows.
/// </para>
/// <para>
/// Resolves as <c>IFlexFieldIndexManager&lt;Content&gt;</c> by ABP's naming convention, since the class
/// name ends with <c>FlexFieldIndexManager</c>.
/// </para>
/// </summary>
public class ContentFlexFieldIndexManager
    : EfCoreFlexFieldIndexManagerBase<ISitesDbContext, Content, ContentFlexFieldIndex>,
      ITransientDependency
{
    protected ICurrentTenant CurrentTenant { get; }

    protected IGuidGenerator GuidGenerator { get; }

    protected override string EntityIdPropertyName => nameof(ContentFlexFieldIndex.ContentId);

    public ContentFlexFieldIndexManager(
        IDbContextProvider<ISitesDbContext> dbContextProvider,
        IFlexFieldProvider<Content> flexFieldProvider,
        IFieldTypeResolver fieldTypeResolver,
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator)
        : base(dbContextProvider, flexFieldProvider, fieldTypeResolver)
    {
        CurrentTenant = currentTenant;
        GuidGenerator = guidGenerator;
    }

    protected override ContentFlexFieldIndex CreateIndexRow(Guid entityId, Guid fieldId, FlexFieldIndexValue value)
    {
        return new ContentFlexFieldIndex(GuidGenerator.Create(), entityId, fieldId, value, CurrentTenant.Id);
    }
}
