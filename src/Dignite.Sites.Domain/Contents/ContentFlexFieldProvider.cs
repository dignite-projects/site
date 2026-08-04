using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Sites.ContentTypes;
using Dignite.Sites.Fields;
using Volo.Abp.DependencyInjection;

namespace Dignite.Sites.Contents;

/// <summary>
/// The one seam Sites implements for FlexFields: given a content, what fields does it have and what are
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

    protected IFieldRepository FieldRepository { get; }

    protected IContentRepository ContentRepository { get; }

    public ContentFlexFieldProvider(
        IContentTypeRepository contentTypeRepository,
        IFieldRepository fieldRepository,
        IContentRepository contentRepository)
    {
        ContentTypeRepository = contentTypeRepository;
        FieldRepository = fieldRepository;
        ContentRepository = contentRepository;
    }

    public virtual async Task<IReadOnlyList<FlexFieldValue>> GetFlexFieldsAsync(
        Content entity,
        CancellationToken cancellationToken = default)
    {
        var contentType = await ContentTypeRepository.FindAsync(entity.ContentTypeId, cancellationToken: cancellationToken);
        if (contentType == null || contentType.Fields.Count == 0)
        {
            return Array.Empty<FlexFieldValue>();
        }

        var definitions = (await FieldRepository.GetListAsync(contentType.GetFieldIds(), cancellationToken))
            .ToDictionary(f => f.Id);

        var values = new List<FlexFieldValue>(contentType.Fields.Count);

        // Content type order, not definition order: this list is also what an editor form and an MCP
        // tool description are built from, and the arrangement is the content type's to decide.
        foreach (var usage in contentType.Fields)
        {
            // A usage whose definition has been deleted is skipped rather than treated as an error. It
            // is reachable in normal operation - deleting a field does not rewrite every content type
            // that referenced it - and the alternative would make every content of an affected type
            // unreadable until someone tidied up.
            if (!definitions.TryGetValue(usage.FieldId, out var definition))
            {
                continue;
            }

            values.Add(new FlexFieldValue(
                definition.ToFlexFieldData(),
                usage.Required,
                usage.Searchable,
                entity.GetField(definition.Name)));
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
