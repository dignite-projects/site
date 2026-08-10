using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Contents;

/// <summary>
/// The one seam Site implements for FlexFields: given a content, what fields does it have and what are
/// their values (总体设计 §8.2).
/// <para>
/// The kernel owns no field or host model of its own, so this is its <i>only</i> way to learn anything
/// about a content's fields - everything it does downstream (validation, index maintenance, query
/// pushdown, rename migration) is driven from what this returns. Three sources are merged here, and
/// keeping them separate is the whole point of the model:
/// </para>
/// <list type="number">
/// <item>the <b>definition</b>, from the tenant's field library - what the field intrinsically is;</item>
/// <item>the <b>usage</b>, from the content's own content type - Required and Searchable <i>here</i>,
/// which the same definition can carry differently in another type;</item>
/// <item>the <b>value</b>, from the content's own bag, keyed by the definition's name.</item>
/// </list>
/// <para>
/// Registered by convention: ABP exposes a class as any interface whose name (less the leading "I") the
/// class name ends with, and <c>ContentFlexFieldProvider</c> ends with <c>FlexFieldProvider</c>, so this
/// resolves as <c>IFlexFieldProvider&lt;Content&gt;</c> with no explicit registration.
/// </para>
/// </summary>
public class ContentFlexFieldProvider : IFlexFieldProvider<Content>, ITransientDependency
{
    protected IContentTypeRepository ContentTypeRepository { get; }

    protected ContentTypeFieldResolver FieldResolver { get; }

    protected IContentRepository ContentRepository { get; }

    public ContentFlexFieldProvider(
        IContentTypeRepository contentTypeRepository,
        ContentTypeFieldResolver fieldResolver,
        IContentRepository contentRepository)
    {
        ContentTypeRepository = contentTypeRepository;
        FieldResolver = fieldResolver;
        ContentRepository = contentRepository;
    }

    public virtual async Task<IReadOnlyList<FlexFieldValue>> GetFlexFieldsAsync(
        Content entity,
        CancellationToken cancellationToken = default)
    {
        var contentType = await ContentTypeRepository.FindAsync(entity.ContentTypeId, cancellationToken: cancellationToken);
        if (contentType == null)
        {
            return Array.Empty<FlexFieldValue>();
        }

        // Sources 1 and 2. The resolver keeps the content type's own order, and drops usages whose
        // definition has since been deleted - both of which this method used to do inline, and both of
        // which the site-schema document handed to an AI client now inherits unchanged.
        var resolved = await FieldResolver.ResolveAsync(contentType, cancellationToken);

        var values = new List<FlexFieldValue>(resolved.Count);

        foreach (var field in resolved)
        {
            // ToFlexFieldData() only ever knows the definition's own DisplayName; ResolvedContentTypeField
            // already carries the correctly-resolved one (usage override, falling back to the definition),
            // so it overwrites it here rather than leaving the two to silently disagree.
            var data = field.Definition.ToFlexFieldData();
            data.DisplayName = field.DisplayName;

            // Source 3, the only one that is this provider's own business.
            values.Add(new FlexFieldValue(
                data,
                field.Usage.Required,
                field.Usage.Searchable,
                entity.GetField(field.Definition.Name)));
        }

        return values;
    }

    /// <summary>
    /// Pages through every content, for <c>IFlexFieldIndexManager.RebuildAsync</c>.
    /// <para>
    /// The ordering has to be stable across calls, which is why it is by id: paging with an unstable
    /// order silently skips rows when they shift between pages, and a rebuild that skips rows produces
    /// exactly the failure the index exists to prevent - a content that no search finds.
    /// </para>
    /// </summary>
    public virtual async Task<IReadOnlyList<Content>> GetPagedEntitiesAsync(
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        return await ContentRepository.GetPagedListByIdAsync(skipCount, maxResultCount, cancellationToken);
    }
}
