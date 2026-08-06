using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.ContentTypes;

/// <summary>
/// Joins a content type's field arrangement to the definitions it references - "definition ∪ usage",
/// the first two of the three sources <c>ContentFlexFieldProvider</c> merges (总体设计 §6.2.4).
/// <para>
/// Two callers need exactly this pair and nothing else would be shared if they each did the join
/// themselves: <c>ContentFlexFieldProvider</c>, which adds a content's stored values as the third source
/// and hands the result to the FlexFields kernel, and the Application layer's site-schema service, which
/// hands the same pair to an AI client as the target shape for generated content. Keeping the join here
/// is what stops those two from drifting - a schema that advertises a field the validator does not
/// enforce (or the reverse) is precisely the failure "schema is the contract" (§1.2) rules out.
/// </para>
/// <para>
/// Read-only by construction. This is the only domain code the MCP tool surface needed, and it adds no
/// second write path.
/// </para>
/// </summary>
public class ContentTypeFieldResolver : DomainService
{
    protected IFieldRepository FieldRepository { get; }

    public ContentTypeFieldResolver(IFieldRepository fieldRepository)
    {
        FieldRepository = fieldRepository;
    }

    /// <summary>
    /// Resolves one content type's arrangement, in the content type's own order.
    /// <para>
    /// Content type order, not definition order: this list is what an editor form, a rendered template
    /// and an MCP tool's target shape are all built from, and the arrangement is the content type's to
    /// decide.
    /// </para>
    /// </summary>
    public virtual async Task<IReadOnlyList<ResolvedContentTypeField>> ResolveAsync(
        ContentType contentType,
        CancellationToken cancellationToken = default)
    {
        if (contentType.Fields.Count == 0)
        {
            return Array.Empty<ResolvedContentTypeField>();
        }

        var definitions = (await FieldRepository.GetListAsync(contentType.GetFieldIds(), cancellationToken))
            .ToDictionary(f => f.Id);

        var resolved = new List<ResolvedContentTypeField>(contentType.Fields.Count);

        foreach (var usage in contentType.Fields)
        {
            // A usage whose definition has been deleted is skipped rather than treated as an error. It
            // is reachable in normal operation - deleting a field does not rewrite every content type
            // that referenced it - and the alternative would make every content of an affected type
            // unreadable until someone tidied up.
            if (definitions.TryGetValue(usage.FieldId, out var definition))
            {
                resolved.Add(new ResolvedContentTypeField(definition, usage));
            }
        }

        return resolved;
    }

    /// <summary>
    /// Resolves several content types in one round trip to the field library.
    /// <para>
    /// The site-schema document walks every content type of every page, and per-type resolution would
    /// make that N queries deep into a tenant's whole site. The definitions are loaded once for the
    /// union of the ids instead; the per-type grouping is then pure in-memory work.
    /// </para>
    /// </summary>
    public virtual async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ResolvedContentTypeField>>> ResolveManyAsync(
        IEnumerable<ContentType> contentTypes,
        CancellationToken cancellationToken = default)
    {
        var types = contentTypes.ToList();

        var fieldIds = types.SelectMany(t => t.GetFieldIds()).Distinct().ToList();

        var definitions = fieldIds.Count == 0
            ? new Dictionary<Guid, Field>()
            : (await FieldRepository.GetListAsync(fieldIds, cancellationToken)).ToDictionary(f => f.Id);

        var result = new Dictionary<Guid, IReadOnlyList<ResolvedContentTypeField>>(types.Count);

        foreach (var contentType in types)
        {
            result[contentType.Id] = contentType.Fields
                .Where(usage => definitions.ContainsKey(usage.FieldId))
                .Select(usage => new ResolvedContentTypeField(definitions[usage.FieldId], usage))
                .ToList();
        }

        return result;
    }
}
