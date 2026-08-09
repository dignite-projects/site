using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Volo.Abp.Domain.Services;

namespace Dignite.Site.ContentTypes;

/// <summary>
/// Creating and changing content types, with the name uniqueness that is scoped to the owning page
/// (总体设计 §2.3).
/// </summary>
public class ContentTypeManager : DomainService
{
    protected IContentTypeRepository ContentTypeRepository { get; }

    protected IPageRepository PageRepository { get; }

    protected IFlexFieldIndexManager<Content> IndexManager { get; }

    public ContentTypeManager(
        IContentTypeRepository contentTypeRepository,
        IPageRepository pageRepository,
        IFlexFieldIndexManager<Content> indexManager)
    {
        ContentTypeRepository = contentTypeRepository;
        PageRepository = pageRepository;
        IndexManager = indexManager;
    }

    public virtual async Task<ContentType> CreateAsync(
        Guid pageId,
        string name,
        string displayName,
        string? description = null,
        IEnumerable<ContentTypeField>? fields = null,
        CancellationToken cancellationToken = default)
    {
        // Resolves the page as a side effect of validating it exists - a content type with a dangling
        // PageId would be invisible to every route, since resolution starts from the page. The result
        // itself is discarded, so no need for the content types GetAsync would otherwise Include by
        // default.
        await PageRepository.GetAsync(pageId, includeDetails: false, cancellationToken: cancellationToken);

        await CheckNameAsync(pageId, name, null, cancellationToken);

        var contentType = new ContentType(
            GuidGenerator.Create(),
            pageId,
            name,
            displayName,
            description,
            fields,
            CurrentTenant.Id);

        return await ContentTypeRepository.InsertAsync(contentType, cancellationToken: cancellationToken);
    }

    public virtual async Task<ContentType> UpdateAsync(
        ContentType contentType,
        string name,
        string displayName,
        string? description = null,
        IEnumerable<ContentTypeField>? fields = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(contentType.Name, name, StringComparison.Ordinal))
        {
            await CheckNameAsync(contentType.PageId, name, contentType.Id, cancellationToken);
            contentType.SetName(name);
        }

        contentType.SetDisplayName(displayName);
        contentType.SetDescription(description);

        // Captured before the arrangement is replaced - this is the only moment the old one still exists.
        var searchableBefore = GetSearchableFieldIds(contentType);

        if (fields != null)
        {
            contentType.SetFields(fields);
        }

        contentType = await ContentTypeRepository.UpdateAsync(contentType, cancellationToken: cancellationToken);

        if (!searchableBefore.SetEquals(GetSearchableFieldIds(contentType)))
        {
            await IndexManager.RebuildAsync(cancellationToken);
        }

        return contentType;
    }

    /// <summary>
    /// Which of this type's usages are searchable. Compared across an update because Searchable is what
    /// decides whether a value is projected into the query index at all - and unlike a value edit,
    /// flipping it changes the index for contents that were not themselves touched.
    /// <para>
    /// Without the rebuild this triggers, marking a field searchable would take effect only for contents
    /// saved afterwards: every existing one would keep no index row for it, so filtering on that field
    /// would quietly return an incomplete result rather than fail. 总体设计 §2.4 names this as one of the
    /// two reindex triggers, and the kernel's own <c>RebuildAsync</c> docs name the same two.
    /// </para>
    /// <para>
    /// Rebuilding is a complete fix here - the kernel re-derives every row from the raw bag value on each
    /// run - but it is tenant-wide rather than scoped to this type, since <c>RebuildAsync</c> is the only
    /// bulk entry point the kernel offers. Correct, and worth the cost on what is a rare schema change.
    /// </para>
    /// </summary>
    protected virtual HashSet<Guid> GetSearchableFieldIds(ContentType contentType)
    {
        return contentType.Fields.Where(f => f.Searchable).Select(f => f.FieldId).ToHashSet();
    }

    protected virtual async Task CheckNameAsync(
        Guid pageId,
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (await ContentTypeRepository.NameExistsAsync(pageId, name, excludedId, cancellationToken))
        {
            throw new ContentTypeNameAlreadyExistException(name);
        }
    }
}
