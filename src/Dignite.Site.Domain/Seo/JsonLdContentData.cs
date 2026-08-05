using System;
using System.Collections.Generic;
using Dignite.Site.ContentTypes;

namespace Dignite.Site.Seo;

/// <summary>
/// What one content contributes to JSON-LD, once its content type has opted into a schema.org type
/// (总体设计 §5.4, GitHub issue #20). Absent (<see cref="HeadMetadata.ContentData"/> is null) when the
/// route matched a bare page, or its content type's <see cref="SchemaOrgType"/> is
/// <see cref="SchemaOrgType.None"/>.
/// </summary>
public class JsonLdContentData
{
    public JsonLdContentData(
        SchemaOrgType schemaType,
        IReadOnlyDictionary<string, object?> propertyValues,
        DateTime publishTime,
        DateTime modifiedTime,
        IReadOnlyList<string> expectedProperties)
    {
        SchemaType = schemaType;
        PropertyValues = propertyValues;
        PublishTime = publishTime;
        ModifiedTime = modifiedTime;
        ExpectedProperties = expectedProperties;
    }

    public SchemaOrgType SchemaType { get; }

    /// <summary>
    /// Keyed by the tenant-chosen <c>ContentTypeField.SchemaProperty</c> string, e.g. <c>"headline"</c> or
    /// <c>"datePublished"</c>. The schema.org vocabulary itself is deliberately not known here - only the
    /// Application layer that builds the actual JSON-LD interprets these keys, so it stays the one place
    /// that vocabulary is defined (总体设计 §5.4).
    /// </summary>
    public IReadOnlyDictionary<string, object?> PropertyValues { get; }

    public DateTime PublishTime { get; }

    public DateTime ModifiedTime { get; }

    /// <summary>
    /// The properties a <see cref="SchemaType"/> node is commonly expected to carry, whether or not this
    /// content supplies them - the input to a local, structural stand-in for "would this pass a Rich
    /// Results check", computed with no call to any external service.
    /// <para>
    /// Which of these actually reached the emitted node is deliberately <b>not</b> decided here: a value
    /// can be present in <see cref="PropertyValues"/> and still be dropped downstream (a relative URL where
    /// an absolute one is required, a number where a string is). Only the layer that builds the node knows
    /// what it accepted, so that layer is the one that subtracts this list down to what is really missing.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> ExpectedProperties { get; }
}
