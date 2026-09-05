using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Volo.Abp.Domain.Services;
using Volo.Abp.Timing;
using Volo.Abp.Validation;

namespace Dignite.Site.Contents;

/// <summary>
/// The write path for contents: the invariants of 总体设计 §2.4/§2.5, plus the two FlexFields calls that
/// have to bracket every save.
/// <para>
/// The ordering below is the part worth reading. Field values are validated <i>before</i> the entity is
/// persisted, and the query index is synchronized <i>after</i>, in the same unit of work. Synchronizing
/// inside the same unit of work is what makes the derived index commit atomically with the value bag it
/// was derived from - do it in a separate transaction and a failure in between leaves the index claiming
/// something the authoritative bag does not say.
/// </para>
/// </summary>
public class ContentManager : DomainService
{
    protected IContentRepository ContentRepository { get; }

    protected IContentTypeRepository ContentTypeRepository { get; }

    protected IFieldRepository FieldRepository { get; }

    protected IPageRepository PageRepository { get; }

    protected IFlexFieldValidator<Content> FlexFieldValidator { get; }

    protected IFlexFieldIndexManager<Content> IndexManager { get; }

    protected IFieldTypeResolver FieldTypeResolver { get; }

    protected IClock Clock { get; }

    public ContentManager(
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        IFieldRepository fieldRepository,
        IPageRepository pageRepository,
        IFlexFieldValidator<Content> flexFieldValidator,
        IFlexFieldIndexManager<Content> indexManager,
        IFieldTypeResolver fieldTypeResolver,
        IClock clock)
    {
        ContentRepository = contentRepository;
        ContentTypeRepository = contentTypeRepository;
        FieldRepository = fieldRepository;
        PageRepository = pageRepository;
        FlexFieldValidator = flexFieldValidator;
        IndexManager = indexManager;
        FieldTypeResolver = fieldTypeResolver;
        Clock = clock;
    }

    public virtual async Task<Content> CreateAsync(
        Guid contentTypeId,
        string cultureName,
        string slug,
        DateTime publishTime,
        ContentStatus status = ContentStatus.Draft,
        IDictionary<string, object?>? fieldValues = null,
        CancellationToken cancellationToken = default)
    {
        CheckPublishSchedule(status, publishTime);

        var contentType = await ContentTypeRepository.GetAsync(contentTypeId, cancellationToken: cancellationToken);

        // Normalize before the uniqueness check, not after: the check has to run against the value that
        // will actually be stored, or "zh-cn" gets waved through against a stored "zh-Hans" row and then
        // lands as a duplicate.
        var normalizedCulture = CultureNameNormalizer.Normalize(cultureName);
        var normalizedSlug = slug ?? string.Empty;

        await CheckSlugRequirementAsync(contentType.PageId, normalizedSlug, cancellationToken);
        await CheckSlugAsync(contentType.PageId, normalizedCulture, normalizedSlug, null, cancellationToken);

        var content = new Content(
            GuidGenerator.Create(),
            contentType.PageId,
            contentTypeId,
            normalizedCulture,
            normalizedSlug,
            publishTime,
            status,
            CurrentTenant.Id);

        await SetFieldValuesAsync(content, contentType, fieldValues, cancellationToken);
        await ValidateFlexFieldsAsync(content, cancellationToken);

        content = await ContentRepository.InsertAsync(content, cancellationToken: cancellationToken);
        await IndexManager.SynchronizeAsync(content, cancellationToken);

        return content;
    }

    public virtual async Task<Content> UpdateAsync(
        Content content,
        string slug,
        DateTime publishTime,
        ContentStatus status,
        IDictionary<string, object?>? fieldValues = null,
        Guid? contentTypeId = null,
        CancellationToken cancellationToken = default)
    {
        CheckPublishSchedule(status, publishTime);

        var effectiveTypeId = contentTypeId ?? content.ContentTypeId;
        var isChangingType = effectiveTypeId != content.ContentTypeId;
        var contentType = await ContentTypeRepository.GetAsync(effectiveTypeId, cancellationToken: cancellationToken);

        // A content type from another page would give this content a URL its page does not own.
        if (contentType.PageId != content.PageId)
        {
            throw new ContentPageInconsistentException(effectiveTypeId);
        }

        var normalizedSlug = slug ?? string.Empty;

        if (!string.Equals(content.Slug, normalizedSlug, StringComparison.Ordinal))
        {
            await CheckSlugRequirementAsync(content.PageId, normalizedSlug, cancellationToken);
            await CheckSlugAsync(content.PageId, content.CultureName, normalizedSlug, content.Id, cancellationToken);
            content.SetSlug(normalizedSlug);
        }

        content.SetContentTypeId(effectiveTypeId);
        content.SetPublishTime(publishTime);
        content.SetStatus(status);

        // A type change re-filters the bag against the NEW type's declared names even when the caller
        // did not resend values. Without this, SetFieldValuesAsync's null-means-"leave alone" shortcut -
        // correct for an ordinary status-only or schedule-only update - would leave the OLD type's values
        // sitting under keys the new type never declared. Nothing downstream would catch it:
        // ValidateFlexFieldsAsync only walks the new type's declared fields, so a stray leftover key is
        // invisible to it and reaches every DTO consumer unfiltered, in direct contradiction of
        // SetFieldValuesAsync's own contract that undeclared keys are dropped, not stored.
        var effectiveFieldValues = fieldValues ?? (isChangingType
            ? content.FlexFields.ToDictionary(pair => pair.Key, pair => (object?)pair.Value)
            : null);

        await SetFieldValuesAsync(content, contentType, effectiveFieldValues, cancellationToken);
        await ValidateFlexFieldsAsync(content, cancellationToken);

        content = await ContentRepository.UpdateAsync(content, cancellationToken: cancellationToken);
        await IndexManager.SynchronizeAsync(content, cancellationToken);

        return content;
    }

    /// <summary>
    /// Replaces the value bag with the values that belong to this content's type.
    /// <para>
    /// Keys the type does not declare are dropped rather than stored. A bag is not a free-form
    /// dictionary - it is the storage for a declared schema, and letting an undeclared key in means a
    /// value nothing validates, nothing indexes and nothing can ever read back by name. Dignite.Cms
    /// filtered the same way, in <c>EntryManager.CheckExtraPropertiesAsync</c>.
    /// </para>
    /// <para>
    /// Passing null leaves the existing values alone, which is what makes a status-only or
    /// schedule-only update possible without resending the whole content.
    /// </para>
    /// </summary>
    protected virtual async Task SetFieldValuesAsync(
        Content content,
        ContentType contentType,
        IDictionary<string, object?>? fieldValues,
        CancellationToken cancellationToken)
    {
        if (fieldValues == null)
        {
            return;
        }

        var declaredFields = (await FieldRepository.GetListAsync(contentType.GetFieldIds(), cancellationToken))
            .ToDictionary(f => f.Name, f => f, StringComparer.Ordinal);

        content.FlexFields.Clear();

        foreach (var (name, value) in fieldValues)
        {
            if (value != null && declaredFields.TryGetValue(name, out var field))
            {
                content.SetField(name, NormalizeFieldValue(field.FieldTypeName, value));
            }
        }
    }

    /// <summary>
    /// Canonicalizes a composite field type's value before it lands in the bag, so what gets persisted
    /// never depends on which casing a particular caller happened to send (see
    /// <see cref="Dignite.Abp.FlexFields.INormalizesValue"/>). A no-op for every field type that has
    /// not opted in - which today is every FlexFields-native scalar type, unchanged from before this
    /// existed.
    /// <para>
    /// <c>FieldTypeResolver.Get</c> can throw for a <c>FieldTypeName</c> no longer registered, but that is
    /// not a new failure mode: <see cref="ValidateFlexFieldsAsync"/>, called right after this method
    /// returns, resolves the exact same name through <c>FlexFieldValidator</c> unconditionally already -
    /// this only reaches the same failure one step sooner in the same call.
    /// </para>
    /// <para>
    /// Fully-qualified on purpose: this used to be Site's own <c>INormalizesValue</c>, deleted once
    /// flex-fields shipped an identical kernel copy at 10.0.0-rc.16 (implemented by both the kernel's
    /// <c>Matrix</c>/<c>Table</c> and Site's own <c>SeoFieldType</c>) - a future Site-local type reusing
    /// this name would otherwise be silently shadowed here rather than failing loudly.
    /// </para>
    /// </summary>
    protected virtual object? NormalizeFieldValue(string fieldTypeName, object? value)
    {
        return FieldTypeResolver.Get(fieldTypeName) is Dignite.Abp.FlexFields.INormalizesValue normalizer
            ? normalizer.Normalize(value)
            : value;
    }

    /// <summary>
    /// Runs every field value through its field type's own rules.
    /// <para>
    /// The kernel returns the errors rather than throwing, so that a caller can decide what to do with
    /// them. Site throws: a content saved with values its own schema rejects is exactly the thing
    /// "schema is the contract" (总体设计 §1.2) promises cannot happen, and the MCP write path in
    /// particular needs a hard, machine-readable failure rather than a partially-valid record.
    /// </para>
    /// </summary>
    protected virtual async Task ValidateFlexFieldsAsync(Content content, CancellationToken cancellationToken)
    {
        var errors = await FlexFieldValidator.ValidateAsync(content, cancellationToken);

        if (errors.Count > 0)
        {
            throw new AbpValidationException(
                "One or more field values are not valid for this content type.",
                errors.ToList());
        }
    }

    /// <summary>
    /// A draft can never carry a future publish time (总体设计 §2.4) - see
    /// <see cref="ContentDraftCannotHaveFuturePublishTimeException"/> for why. Scheduling for the future
    /// is done by publishing with a future <c>PublishTime</c> instead, which the read path already keeps
    /// hidden until due.
    /// </summary>
    protected virtual void CheckPublishSchedule(ContentStatus status, DateTime publishTime)
    {
        if (status == ContentStatus.Draft && publishTime > Clock.Now)
        {
            throw new ContentDraftCannotHaveFuturePublishTimeException(publishTime);
        }
    }

    protected virtual async Task CheckSlugAsync(
        Guid pageId,
        string cultureName,
        string slug,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (await ContentRepository.SlugExistsAsync(pageId, cultureName, slug, excludedId, cancellationToken))
        {
            throw new ContentSlugAlreadyExistException(cultureName, slug);
        }
    }

    /// <summary>
    /// Enforces what the owning page's route (总体设计 §3.3) says about slugs, ahead of
    /// <see cref="CheckSlugAsync"/> - a structural rule is cheaper to fail on than a uniqueness query, and
    /// more fundamental. A route with neither <c>{slug}</c> nor <c>{slug?}</c> never allows a non-empty
    /// slug; a route with a mandatory <c>{slug}</c> never allows an empty one. Whether an <i>allowed</i>
    /// empty slug is already taken by another content is a separate question, one <see cref="CheckSlugAsync"/>
    /// answers - this method does not duplicate it.
    /// <para>
    /// A rule change here is prospective, not retroactive, the same as changing a page's route itself:
    /// existing contents are not re-validated when the route changes underneath them, only the next write
    /// to one of them is.
    /// </para>
    /// </summary>
    protected virtual async Task CheckSlugRequirementAsync(Guid pageId, string slug, CancellationToken cancellationToken)
    {
        var page = await PageRepository.GetAsync(pageId, includeDetails: false, cancellationToken);

        if (!PageRoute.HasSlug(page.Route))
        {
            if (slug.Length > 0)
            {
                throw new ContentSlugNotAllowedException(page.Route, slug);
            }

            return;
        }

        if (!PageRoute.IsSlugOptional(page.Route) && slug.Length == 0)
        {
            throw new ContentSlugRequiredException(page.Route);
        }
    }
}
