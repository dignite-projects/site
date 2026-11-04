using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Common;
using Dignite.Site.Contents;
using Dignite.Site.ContentTypes;
using Dignite.Site.Pages;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Contents;

[Authorize(SiteAdminPermissions.Contents.Default)]
public class ContentAdminAppService : SiteAdminAppService, IContentAdminAppService
{
    protected IContentRepository ContentRepository { get; }

    protected ContentManager ContentManager { get; }

    protected IPageRepository PageRepository { get; }

    protected IContentTypeRepository ContentTypeRepository { get; }

    public ContentAdminAppService(
        IContentRepository contentRepository,
        ContentManager contentManager,
        IPageRepository pageRepository,
        IContentTypeRepository contentTypeRepository)
    {
        ContentRepository = contentRepository;
        ContentManager = contentManager;
        PageRepository = pageRepository;
        ContentTypeRepository = contentTypeRepository;
    }

    public virtual async Task<ContentDto> GetAsync(Guid id)
    {
        var content = await ContentRepository.GetAsync(id);
        return await MapToDtoAsync(content);
    }

    public virtual async Task<ContentDto?> FindBySlugAsync(Guid pageId, string cultureName, string slug)
    {
        // A null slug is rejected rather than coerced. Empty is a real address - the page's own single
        // content (总体设计 §2.4) - so silently reading "caller omitted it" as "caller meant empty" would
        // answer a question nobody asked, with a 200 and the wrong content rather than an error.
        if (slug == null)
        {
            throw new AbpValidationException(
                "A slug is required to address a content.",
                new List<ValidationResult>
                {
                    new(
                        "Pass an empty string to address the page's own single content. Omitting the slug "
                        + "entirely is not the same thing and is not a valid address.",
                        new[] { nameof(slug) })
                });
        }

        // TryNormalize, not Normalize: the strict form throws on anything CultureInfo does not recognize,
        // and this is a "find or null" lookup - a culture this site could never have stored is a miss, not
        // a server error. The normalizer itself is the same one the write path runs
        // (ContentManager.CreateAsync), applied to the address rather than to the value being stored, so
        // the address matches what was actually persisted. That is not the "second normalization point"
        // 总体设计 §2.4 warns about: that is about two places deciding what to *store*.
        if (!CultureNameNormalizer.TryNormalize(cultureName, out var normalizedCulture))
        {
            return null;
        }

        var content = await ContentRepository.FindBySlugAsync(pageId, normalizedCulture, slug);

        return content == null ? null : await MapToDtoAsync(content);
    }

    public virtual async Task<PagedResultDto<ContentDto>> GetListAsync(GetContentListInput input)
    {
        var totalCount = await ContentRepository.GetCountAsync(
            pageId: input.PageId, cultureName: input.CultureName, contentTypeId: input.ContentTypeId,
            status: input.Status, publishedBefore: input.PublishedBefore, publishedAfter: input.PublishedAfter,
            filter: input.Filter, flexFieldConditions: input.FlexFieldConditions);

        var contents = await ContentRepository.GetListAsync(
            pageId: input.PageId, cultureName: input.CultureName, contentTypeId: input.ContentTypeId,
            status: input.Status, publishedBefore: input.PublishedBefore, publishedAfter: input.PublishedAfter,
            filter: input.Filter, flexFieldConditions: input.FlexFieldConditions,
            maxResultCount: input.MaxResultCount, skipCount: input.SkipCount, sorting: input.Sorting);

        return new PagedResultDto<ContentDto>(totalCount, await MapToDtosAsync(contents));
    }

    [Authorize(SiteAdminPermissions.Contents.Create)]
    public virtual async Task<ContentDto> CreateAsync(CreateContentDto input)
    {
        var content = await ContentManager.CreateAsync(
            input.ContentTypeId, input.CultureName, input.Slug, input.PublishTime, input.Status,
            input.FieldValues);

        return await MapToDtoAsync(content);
    }

    [Authorize(SiteAdminPermissions.Contents.Update)]
    public virtual async Task<ContentDto> UpdateAsync(Guid id, UpdateContentDto input)
    {
        var content = await ContentRepository.GetAsync(id);

        content = await ContentManager.UpdateAsync(
            content, input.Slug, input.PublishTime, input.Status, input.FieldValues, input.ContentTypeId);

        return await MapToDtoAsync(content);
    }

    [Authorize(SiteAdminPermissions.Contents.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Nothing else references a content by FK except its own (cascading) query-index rows.
        await ContentRepository.DeleteAsync(id);
    }

    protected virtual async Task<ContentDto> MapToDtoAsync(Content content)
    {
        return (await MapToDtosAsync(new[] { content })).Single();
    }

    /// <summary>
    /// The contents as DTOs, each with the names of its page and content type: looked up once for the whole list, from the
    /// repositories, so that reading a content needs the permission to read contents and no more - a client that addresses
    /// everything by name is told the names of what it is allowed to read.
    /// </summary>
    protected virtual async Task<List<ContentDto>> MapToDtosAsync(IReadOnlyCollection<Content> contents)
    {
        if (contents.Count == 0)
        {
            return new List<ContentDto>();
        }

        var pageIds = contents.Select(content => content.PageId).Distinct().ToList();
        var pageNames = (await PageRepository.GetListAsync(pageIds)).ToDictionary(page => page.Id, page => page.Name);

        var contentTypeNames = new Dictionary<Guid, string>();
        foreach (var pageId in pageIds)
        {
            foreach (var contentType in await ContentTypeRepository.GetListByPageAsync(pageId))
            {
                contentTypeNames[contentType.Id] = contentType.Name;
            }
        }

        return contents
            .Select(content =>
            {
                var dto = ObjectMapper.Map<Content, ContentDto>(content);
                dto.FieldValues = content.FlexFields.ToValueDictionary();
                dto.PageName = pageNames.GetValueOrDefault(content.PageId) ?? string.Empty;
                dto.ContentTypeName = contentTypeNames.GetValueOrDefault(content.ContentTypeId) ?? string.Empty;
                return dto;
            })
            .ToList();
    }
}
