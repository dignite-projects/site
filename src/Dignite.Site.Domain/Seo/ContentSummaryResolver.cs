using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.Text;
using Dignite.FlexFields.Site.Seo;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.Seo;

/// <summary>
/// Works out what to call a content and how to describe it, when the platform has no "title" field to ask.
/// <para>
/// That absence is by design: <c>title</c> and <c>body</c> in a tenant's field library are ordinary tenant
/// data, not platform vocabulary (总体设计 §2.4), so nothing may hard-code those names. What the platform
/// does recognize is the seeded SEO field (§5.3, GitHub issue #14): its <c>MetaDescription</c> is the
/// description, and the only one. A content without one has <b>no description</b> - a text field that happens to
/// sit after the title is not the author saying what the page is about (a year, a category, a price became a
/// page's meta description that way), and a missing description is something an author can see and fix.
/// </para>
/// <para>
/// <b>The title is still guessed</b> when the SEO field has none: the first text field with a value, then the
/// slug. A page cannot go without a title, and this is the one guess left; GitHub issue #20 adds an explicit field
/// mapping to <c>ContentType</c> for JSON-LD, which is what the title should read once it exists.
/// </para>
/// </summary>
public class ContentSummaryResolver : DomainService
{
    protected IContentTypeRepository ContentTypeRepository { get; }

    protected IFieldRepository FieldRepository { get; }

    public ContentSummaryResolver(
        IContentTypeRepository contentTypeRepository,
        IFieldRepository fieldRepository)
    {
        ContentTypeRepository = contentTypeRepository;
        FieldRepository = fieldRepository;
    }

    /// <summary>
    /// Loads what every content under one page needs, once. Both queries are small - a page's content
    /// types, and the tenant's field library - and resolving them per content instead would be one round
    /// trip per feed item.
    /// </summary>
    public virtual async Task<ContentSummaryLookup> CreateLookupAsync(
        Guid pageId,
        CancellationToken cancellationToken = default)
    {
        var contentTypes = await ContentTypeRepository.GetListByPageAsync(pageId, cancellationToken);
        return await CreateLookupAsync(contentTypes, cancellationToken);
    }

    /// <summary>
    /// Same as the <see cref="Guid"/> overload, for a caller that already holds the page's content types -
    /// typically <c>Page.ContentTypes</c> fetched with <c>includeDetails: true</c> - and can skip the query.
    /// </summary>
    public virtual async Task<ContentSummaryLookup> CreateLookupAsync(
        IEnumerable<ContentType> contentTypes,
        CancellationToken cancellationToken = default)
    {
        var fields = await FieldRepository.GetListAsync(cancellationToken: cancellationToken);

        var fieldsById = fields.ToDictionary(f => f.Id);
        var textFieldNames = new Dictionary<Guid, IReadOnlyList<string>>();

        foreach (var contentType in contentTypes)
        {
            textFieldNames[contentType.Id] = contentType.Fields
                .OrderBy(f => f.Order)
                .Select(f => fieldsById.TryGetValue(f.FieldId, out var field) ? field : null)
                .Where(field => field != null
                                && string.Equals(field.FieldTypeName, TextFieldType.ControlName, StringComparison.Ordinal))
                .Select(field => field!.Name)
                .ToList();
        }

        var seoField = fields.FirstOrDefault(
            f => string.Equals(f.Name, SeoFieldNames.FieldName, StringComparison.Ordinal));

        return new ContentSummaryLookup(textFieldNames, seoField?.Name);
    }

    /// <summary>
    /// The title - the SEO field's <c>MetaTitle</c>, else the first text field with a value, else
    /// <paramref name="fallbackTitle"/> - and the description: the SEO field's <c>MetaDescription</c> and nothing else.
    /// </summary>
    /// <param name="fallbackTitle">
    /// Used when the content has neither an SEO title nor any text field with a value - typically the
    /// content's slug, or its page's display name for the empty-slug case where there is no slug either.
    /// </param>
    public virtual ContentSummary Resolve(Content content, ContentSummaryLookup lookup, string fallbackTitle)
    {
        var seoValue = ReadSeoValue(content, lookup);

        var textFieldNames = lookup.GetTextFieldNames(content.ContentTypeId);

        string? title = NullIfBlank(seoValue?.MetaTitle);

        // Only what the author wrote: nothing is taken from another field.
        string? summary = NullIfBlank(seoValue?.MetaDescription);

        if (title == null)
        {
            foreach (var fieldName in textFieldNames)
            {
                title = NullIfBlank(ReadText(content, fieldName));
                if (title != null)
                {
                    break;
                }
            }
        }

        return new ContentSummary(title ?? fallbackTitle, summary);
    }

    protected virtual SeoFieldValue? ReadSeoValue(Content content, ContentSummaryLookup lookup)
    {
        if (lookup.SeoFieldName == null || !content.HasField(lookup.SeoFieldName))
        {
            return null;
        }

        try
        {
            return content.GetField<SeoFieldValue>(lookup.SeoFieldName);
        }
        catch (Exception ex)
        {
            // Same "fail open and log" stance as NoIndexRecognizer: one content whose stored value no
            // longer matches its field type must not take a whole feed down.
            Logger.LogWarning(
                ex,
                "Content {ContentId}'s '{FieldName}' value could not be read as an SEO field value; ignoring it.",
                content.Id, lookup.SeoFieldName);
            return null;
        }
    }

    /// <summary>
    /// Reads a field's value only if it really is text. Deliberately does not go through
    /// <c>GetField&lt;string&gt;</c>: that would throw on a stored number or object, and a field whose
    /// type was changed after values were written is exactly the case this has to survive.
    /// </summary>
    protected virtual string? ReadText(Content content, string fieldName)
    {
        return content.GetField(fieldName) switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null
        };
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
