using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Sites.Contents;
using Dignite.Sites.Fields;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace Dignite.Sites.Seo;

/// <summary>
/// Recognizes the one platform semantic the SEO field carries (总体设计 §5.3): whether a content is
/// <c>noindex</c>. The single place the sitemap generator (#15) and the <c>&lt;head&gt;</c> renderer (#13)
/// both call - neither should re-derive this on its own, or they risk disagreeing.
/// <para>
/// Resolves the field by <see cref="SeoFieldNames.FieldName"/>, never by assuming the bag key is literally
/// <c>"noindex"</c> - the value lives inside the composite <see cref="SeoFieldValue"/> under the seeded
/// field's own name, and that name is what <c>FieldManager</c> protects from renaming.
/// </para>
/// </summary>
public class NoIndexRecognizer : DomainService
{
    protected IFieldRepository FieldRepository { get; }

    public NoIndexRecognizer(IFieldRepository fieldRepository)
    {
        FieldRepository = fieldRepository;
    }

    /// <summary>
    /// Resolves the current tenant's seeded SEO field, once. Split out from <see cref="IsNoIndexAsync"/> so
    /// a batch caller iterating many contents (the sitemap generator) can resolve it a single time and
    /// reuse it via <see cref="IsNoIndex"/>, instead of one lookup per content.
    /// </summary>
    public virtual async Task<Field?> FindFieldAsync(CancellationToken cancellationToken = default)
    {
        return await FieldRepository.FindByNameAsync(SeoFieldNames.FieldName, cancellationToken);
    }

    /// <summary>
    /// Whether <paramref name="content"/> is marked noindex, given an already-resolved
    /// <paramref name="seoField"/> (or <see langword="null"/> if none exists for this tenant).
    /// <para>
    /// Fails open (not noindex) whenever the answer cannot be determined, and logs why: the field has been
    /// deleted or not yet seeded (<paramref name="seoField"/> is <see langword="null"/>), or the stored
    /// value cannot be read as <see cref="SeoFieldValue"/> - its shape is checked at write time by
    /// <see cref="SeoFieldType.Validate"/>, but <c>FieldManager.UpdateAsync</c> still allows the field's
    /// <c>FieldTypeName</c> to be changed afterwards, which can leave a stored value that no longer matches.
    /// A sitemap run should not fail, and should not silently exclude unrelated content, over one content's
    /// unreadable value - it should keep that content indexed and let the anomaly surface as a log entry
    /// instead.
    /// </para>
    /// </summary>
    public virtual bool IsNoIndex(Content content, Field? seoField)
    {
        if (seoField == null)
        {
            return false;
        }

        try
        {
            return content.GetField(seoField.Name, new SeoFieldValue()).NoIndex;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Content {ContentId}'s '{FieldName}' value could not be read as an SEO field value; treating it as not noindex.",
                content.Id, seoField.Name);
            return false;
        }
    }

    /// <summary>Convenience for a single-content caller, e.g. rendering one page's <c>&lt;head&gt;</c>.</summary>
    public virtual async Task<bool> IsNoIndexAsync(Content content, CancellationToken cancellationToken = default)
    {
        var seoField = await FindFieldAsync(cancellationToken);
        return IsNoIndex(content, seoField);
    }
}
