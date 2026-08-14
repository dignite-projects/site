using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Common;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Dignite.Site.Seo;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Timing;

namespace Dignite.Site.Public.Contents;

/// <summary>Published content only, enforced on every path - by id, by slug, in lists, and in translations.</summary>
public class ContentPublicAppService : PublicAppService, IContentPublicAppService
{
    protected IContentRepository ContentRepository { get; }

    protected IPageRepository PageRepository { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    public ContentPublicAppService(
        IContentRepository contentRepository, IPageRepository pageRepository, SiteUrlBuilder urlBuilder)
    {
        ContentRepository = contentRepository;
        PageRepository = pageRepository;
        UrlBuilder = urlBuilder;
    }

    public virtual async Task<ContentDto> GetAsync(Guid id)
    {
        var content = await ContentRepository.GetAsync(id);
        EnsurePublished(content);

        var page = await PageRepository.FindAsync(content.PageId);
        var urlContext = await UrlBuilder.CreateContextAsync();
        return MapToDto(content, page, urlContext);
    }

    public virtual async Task<ContentDto> GetBySlugAsync(Guid pageId, string cultureName, string slug)
    {
        var content = await ContentRepository.FindBySlugAsync(pageId, cultureName, slug);

        if (content == null || !content.IsPublished(Clock.Now))
        {
            throw new EntityNotFoundException(typeof(Content));
        }

        var page = await PageRepository.FindAsync(pageId);
        var urlContext = await UrlBuilder.CreateContextAsync();
        return MapToDto(content, page, urlContext);
    }

    public virtual async Task<PagedResultDto<ContentDto>> GetListAsync(GetContentListInput input)
    {
        var asOf = Clock.Now;

        // input.PublishedBefore can only ever narrow this - never widen it past "now" - regardless of what
        // a caller passes, so a scheduled-future content (Status=Published, PublishTime still ahead of
        // "now" - 总体设计 §2.4) never surfaces early just because a caller's own filter window happens to
        // reach past it. input.PublishedAfter needs no such cap: a lower bound cannot reveal anything that
        // was not already visible.
        var effectiveBefore = input.PublishedBefore is { } before && before < asOf ? before : asOf;

        var totalCount = await ContentRepository.GetCountAsync(
            pageId: input.PageId, cultureName: input.CultureName, contentTypeId: input.ContentTypeId,
            status: ContentStatus.Published, publishedBefore: effectiveBefore, publishedAfter: input.PublishedAfter,
            filter: input.Filter, flexFieldConditions: input.FlexFieldConditions);

        var contents = await ContentRepository.GetListAsync(
            pageId: input.PageId, cultureName: input.CultureName, contentTypeId: input.ContentTypeId,
            status: ContentStatus.Published, publishedBefore: effectiveBefore, publishedAfter: input.PublishedAfter,
            filter: input.Filter, flexFieldConditions: input.FlexFieldConditions, maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount, sorting: input.Sorting);

        // Batched by distinct PageId - not one lookup per item - regardless of whether the caller filtered
        // to a single page (the common case for a page render) or left it open.
        var pageIds = contents.Select(c => c.PageId).Distinct().ToList();
        var pagesById = (await PageRepository.GetListAsync(pageIds)).ToDictionary(p => p.Id);
        var urlContext = await UrlBuilder.CreateContextAsync();

        return new PagedResultDto<ContentDto>(
            totalCount,
            contents.Select(c => MapToDto(c, pagesById.GetValueOrDefault(c.PageId), urlContext)).ToList());
    }

    public virtual async Task<ListResultDto<ContentDto>> GetTranslationsAsync(
        Guid pageId, Guid contentTypeId, string slug)
    {
        var translations = await ContentRepository.GetTranslationsAsync(pageId, contentTypeId, slug);
        var asOf = Clock.Now;

        var page = await PageRepository.FindAsync(pageId);
        var urlContext = await UrlBuilder.CreateContextAsync();

        return new ListResultDto<ContentDto>(
            translations.Where(c => c.IsPublished(asOf)).Select(c => MapToDto(c, page, urlContext)).ToList());
    }

    protected virtual void EnsurePublished(Content content)
    {
        if (!content.IsPublished(Clock.Now))
        {
            throw new EntityNotFoundException(typeof(Content), content.Id);
        }
    }

    /// <summary>
    /// <paramref name="page"/> is null only in the practically-unreachable case that this content's own
    /// page could not be found (e.g. a narrow admin-delete race) - <see cref="ContentDto.Url"/> comes back
    /// empty rather than throwing, since the rest of the content read is still perfectly valid.
    /// </summary>
    protected virtual ContentDto MapToDto(Content content, Page? page, SiteUrlContext urlContext)
    {
        var dto = ObjectMapper.Map<Content, ContentDto>(content);
        dto.FieldValues = content.FlexFields.ToValueDictionary();
        dto.Url = page != null ? UrlBuilder.BuildContentPath(urlContext, page, content) : string.Empty;
        return dto;
    }
}
