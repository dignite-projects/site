using System;
using Dignite.Abp.FlexFields;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Contents;

/// <summary>
/// One content record, in one language (总体设计 §2.4).
/// <para>
/// <b>One row is one language.</b> A post that exists in English and Chinese is two rows, and they are
/// recognized as the same content by the natural key <c>(PageId, ContentTypeId, Slug)</c> - there is no
/// translation-group id. <c>hreflang</c> and the language switcher aggregate on that key directly, which
/// is why they come out almost free (§5.5) and why the grouping can never drift out of step with the
/// data. The price, accepted deliberately, is that a content's slug is the same in every language.
/// </para>
/// <para>
/// <b>Both owners are stored.</b> <c>PageId</c> is derivable from <c>ContentTypeId</c>, since a content
/// type already belongs to a page - but "everything under this page" is the highest-frequency query
/// there is (list views, sitemap, route resolution), and it should not pay for a join every time.
/// Dignite.Cms's <c>Entry</c> stores both for the same reason.
/// </para>
/// <para>
/// <b>Four system fields, and only four.</b> <see cref="CultureName"/>, <see cref="Slug"/>,
/// <see cref="Status"/> and <see cref="PublishTime"/> are here because platform mechanisms hard-depend on
/// them - routing, the publication workflow, scheduled publishing. Everything else a content carries -
/// title, body, cover, SEO metadata - is a business field defined in the field library and stored in
/// <see cref="FlexFields"/>. The test is whether one content type could need it and another not; if so,
/// it is a business field.
/// </para>
/// </summary>
public class Content : FullAuditedAggregateRoot<Guid>, IHasFlexFields, IMultiTenant
{
    protected Content()
    {
        FlexFields = new FlexFieldDictionary();
    }

    public Content(
        Guid id,
        Guid pageId,
        Guid contentTypeId,
        string cultureName,
        string slug,
        DateTime publishTime,
        ContentStatus status = ContentStatus.Draft,
        Guid? tenantId = null)
        : base(id)
    {
        PageId = pageId;
        ContentTypeId = contentTypeId;
        SetCultureName(cultureName);
        SetSlug(slug);
        PublishTime = publishTime;
        Status = status;
        TenantId = tenantId;

        FlexFields = new FlexFieldDictionary();
    }

    /// <summary>Which route this content sits under. Denormalized from the content type - see the type remarks.</summary>
    public virtual Guid PageId { get; protected set; }

    /// <summary>Which shape this content has: what fields it carries, and how a front end should render it.</summary>
    public virtual Guid ContentTypeId { get; protected set; }

    /// <summary>
    /// The language of this row, as a <c>CultureInfo.Name</c>-form BCP 47 tag. Always assigned through
    /// <see cref="SetCultureName"/>, never directly - see <see cref="CultureNameNormalizer"/> for what
    /// an unnormalized value breaks.
    /// </summary>
    public virtual string CultureName { get; protected set; } = default!;

    /// <summary>
    /// The routing key within the page. Empty is legitimate and meaningful: it is the single content of
    /// a page that carries one (a home or "about" page), whose URL is then the page route itself.
    /// </summary>
    public virtual string Slug { get; protected set; } = default!;

    /// <summary>
    /// When this content becomes public. Read together with <see cref="Status"/>: scheduling for the
    /// future is <see cref="ContentStatus.Published"/> with a <see cref="PublishTime"/> that has not
    /// arrived yet - <see cref="IsPublished"/> and the repository's query filters already keep it hidden
    /// until then, with no extra moving part. <see cref="ContentStatus.Draft"/> can never carry a future
    /// <see cref="PublishTime"/>; <c>ContentManager</c> rejects that combination outright, because a
    /// draft is not publicly reachable regardless of the time, so a future date on one is not a schedule
    /// anything would ever act on.
    /// </summary>
    public virtual DateTime PublishTime { get; protected set; }

    public virtual ContentStatus Status { get; protected set; }

    /// <summary>
    /// The authoritative store for every business field's value, keyed by <c>Field.Name</c>
    /// (总体设计 §2.4, FlexFields' "Y" design).
    /// <para>
    /// This is FlexFields' own dictionary, deliberately <b>not</b> ABP's <c>ExtraProperties</c> - kept
    /// separate from the shared bag so nothing else can collide with a tenant's field names.
    /// </para>
    /// <para>
    /// Reading a whole content touches this column and nothing else. The typed
    /// <c>ContentFlexFieldIndex</c> table alongside it is derived, never authoritative, and exists only
    /// so "find contents where field X matches Y" can run as SQL. That is the fix for Dignite.Cms's
    /// worst query path, where a filter on a custom field fell back to <c>AsEnumerable()</c> and
    /// filtered the whole table in memory.
    /// </para>
    /// </summary>
    public virtual FlexFieldDictionary FlexFields { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>
    /// Normalizes and assigns the language tag. Normalizing here rather than trusting callers is what
    /// keeps <c>zh-CN</c> and <c>zh-cn</c> from both reaching the table and splitting a translation
    /// group in half.
    /// </summary>
    public virtual void SetCultureName(string cultureName)
    {
        CultureName = CultureNameNormalizer.Normalize(cultureName);
    }

    /// <summary>
    /// Assigns the slug. Null is coerced to empty, since empty is the real "no slug" value here, and the
    /// column is not nullable.
    /// </summary>
    public virtual void SetSlug(string? slug)
    {
        var value = slug ?? string.Empty;
        Check.Length(value, nameof(slug), ContentConsts.MaxSlugLength);
        if (!SlugNormalizer.IsWellFormed(value))
        {
            throw new InvalidValueFormatException(nameof(Slug), value);
        }

        Slug = value;
    }

    public virtual void SetStatus(ContentStatus status)
    {
        Status = status;
    }

    public virtual void SetPublishTime(DateTime publishTime)
    {
        PublishTime = publishTime;
    }

    /// <summary>
    /// Moves this content to a different shape within the same page. The page cannot change here: that
    /// would change the content's URL and is a move, which belongs in <see cref="ContentManager"/> where
    /// the new page's slug uniqueness can be re-checked.
    /// </summary>
    public virtual void SetContentTypeId(Guid contentTypeId)
    {
        ContentTypeId = contentTypeId;
    }

    /// <summary>
    /// Whether this content is publicly readable at <paramref name="asOf"/>: published, and its publish
    /// time has arrived. The single definition of "live", shared by route resolution, list queries and
    /// the sitemap so they cannot disagree about what the public can see.
    /// </summary>
    public virtual bool IsPublished(DateTime asOf)
    {
        return Status == ContentStatus.Published && PublishTime <= asOf;
    }
}
