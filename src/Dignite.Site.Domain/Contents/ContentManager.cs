using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Fields;
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

    protected IFlexFieldValidator<Content> FlexFieldValidator { get; }

    protected IFlexFieldIndexManager<Content> IndexManager { get; }

    protected IClock Clock { get; }

    public ContentManager(
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        IFieldRepository fieldRepository,
        IFlexFieldValidator<Content> flexFieldValidator,
        IFlexFieldIndexManager<Content> indexManager,
        IClock clock)
    {
        ContentRepository = contentRepository;
        ContentTypeRepository = contentTypeRepository;
        FieldRepository = fieldRepository;
        FlexFieldValidator = flexFieldValidator;
        IndexManager = indexManager;
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
        var contentType = await ContentTypeRepository.GetAsync(effectiveTypeId, cancellationToken: cancellationToken);

        // A content type from another page would give this content a URL its page does not own.
        if (contentType.PageId != content.PageId)
        {
            throw new ContentPageInconsistentException(effectiveTypeId);
        }

        var normalizedSlug = slug ?? string.Empty;

        if (!string.Equals(content.Slug, normalizedSlug, StringComparison.Ordinal))
        {
            await CheckSlugAsync(content.PageId, content.CultureName, normalizedSlug, content.Id, cancellationToken);
            content.SetSlug(normalizedSlug);
        }

        content.SetContentTypeId(effectiveTypeId);
        content.SetPublishTime(publishTime);
        content.SetStatus(status);

        await SetFieldValuesAsync(content, contentType, fieldValues, cancellationToken);
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

        var declaredNames = (await FieldRepository.GetListAsync(contentType.GetFieldIds(), cancellationToken))
            .Select(f => f.Name)
            .ToHashSet(StringComparer.Ordinal);

        content.FlexFields.Clear();

        foreach (var (name, value) in fieldValues)
        {
            if (value != null && declaredNames.Contains(name))
            {
                content.SetField(name, value);
            }
        }
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
}
