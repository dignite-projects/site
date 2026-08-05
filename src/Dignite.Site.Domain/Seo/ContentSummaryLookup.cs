using System;
using System.Collections.Generic;

namespace Dignite.Site.Seo;

/// <summary>
/// The per-page data <see cref="ContentSummaryResolver"/> needs, resolved once for a whole batch.
/// </summary>
public class ContentSummaryLookup
{
    private static readonly IReadOnlyList<string> None = Array.Empty<string>();

    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<string>> _textFieldNamesByContentType;

    public ContentSummaryLookup(
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> textFieldNamesByContentType,
        string? seoFieldName)
    {
        _textFieldNamesByContentType = textFieldNamesByContentType;
        SeoFieldName = seoFieldName;
    }

    /// <summary>
    /// The seeded SEO field's name, or <see langword="null"/> if this tenant has none. Resolved by name
    /// rather than by a shared id, for the reason <c>SeoFieldNames</c> documents.
    /// </summary>
    public string? SeoFieldName { get; }

    /// <summary>
    /// The names of a content type's text fields, in the order the type arranges them. Empty for a type
    /// that has none, or one that is not under the page this lookup was built for.
    /// </summary>
    public IReadOnlyList<string> GetTextFieldNames(Guid contentTypeId)
    {
        return _textFieldNamesByContentType.TryGetValue(contentTypeId, out var names) ? names : None;
    }
}
