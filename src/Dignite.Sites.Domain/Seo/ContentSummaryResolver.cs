using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.Text;
using Dignite.Sites.ContentTypes;
using Dignite.Sites.Contents;
using Dignite.Sites.Fields;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace Dignite.Sites.Seo;

/// <summary>
/// Works out what to call a content and how to describe it, when the platform has no "title" field to ask.
/// <para>
/// That absence is by design: <c>title</c> and <c>body</c> in a tenant's field library are ordinary tenant
/// data, not platform vocabulary (总体设计 §2.4), so nothing may hard-code those names. What the platform
/// does recognize is the seeded SEO field (§5.3, GitHub issue #14) - so its <c>MetaTitle</c> and
/// <c>MetaDescription</c> are the authoritative answer whenever a content type pulled it in, and the first
/// text field on the type is the fallback when it did not.
/// </para>
/// <para>
/// <b>This is a stopgap and should be replaced.</b> GitHub issue #20 adds an explicit field mapping to
/// <c>ContentType</c> for JSON-LD; once that exists, this resolver should read it instead of guessing at
/// field order.
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
    /// Resolves both at once, because they interact: whichever text field supplies the title must not also
    /// supply the summary - <b>even when the title actually shown comes from SEO's <c>MetaTitle</c>
    /// instead</b>. The first field with a value is always that field, its text is never eligible for the
    /// summary, and only the fields after it are - otherwise a content with a custom SEO title but no
    /// custom description would get its own title text duplicated as the description, and the real body
    /// field would never be read at all.
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
        string? summary = NullIfBlank(seoValue?.MetaDescription);
        var titleFieldConsumed = false;

        foreach (var fieldName in textFieldNames)
        {
            if (title != null && summary != null)
            {
                break;
            }

            var text = NullIfBlank(ReadText(content, fieldName));
            if (text == null)
            {
                continue;
            }

            if (!titleFieldConsumed)
            {
                titleFieldConsumed = true;
                title ??= text;
                continue;
            }

            summary ??= text.Length > FeedConsts.MaxSummaryLength
                ? text[..FeedConsts.MaxSummaryLength]
                : text;
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
