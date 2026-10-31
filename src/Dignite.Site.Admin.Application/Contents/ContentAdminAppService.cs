using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Common;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Contents;

/// <summary>
/// Every content, whatever its status - unlike the Public surface. <see cref="ContentDto.Url"/> is the same
/// relative, language-prefixed path the Public surface returns (<c>/blog/my-trip</c>, <c>/zh-Hans/about</c>),
/// for a draft too: it is where the content will be once it is public, which is what an editor previewing it
/// needs.
/// </summary>
[Authorize(SiteAdminPermissions.Contents.Default)]
public class ContentAdminAppService : SiteAdminAppService, IContentAdminAppService
{
    protected IContentRepository ContentRepository { get; }

    protected ContentManager ContentManager { get; }

    protected IPageRepository PageRepository { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    public ContentAdminAppService(
        IContentRepository contentRepository,
        ContentManager contentManager,
        IPageRepository pageRepository,
        SiteUrlBuilder urlBuilder)
    {
        ContentRepository = contentRepository;
        ContentManager = contentManager;
        PageRepository = pageRepository;
        UrlBuilder = urlBuilder;
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

        // Batched by distinct PageId, as the Public list does - not one page lookup per item.
        var pageIds = contents.Select(c => c.PageId).Distinct().ToList();
        var pagesById = (await PageRepository.GetListAsync(pageIds)).ToDictionary(p => p.Id);
        var urlContext = await TryCreateUrlContextAsync();

        return new PagedResultDto<ContentDto>(
            totalCount,
            contents.Select(c => MapToDto(c, pagesById.GetValueOrDefault(c.PageId), urlContext)).ToList());
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

    /// <summary>Maps one content, looking its page up for <see cref="ContentDto.Url"/>.</summary>
    protected virtual async Task<ContentDto> MapToDtoAsync(Content content)
    {
        var page = await PageRepository.FindAsync(content.PageId, includeDetails: false);
        return MapToDto(content, page, await TryCreateUrlContextAsync());
    }

    /// <summary>
    /// <see cref="ContentDto.Url"/> is composed exactly as <c>ContentPublicAppService.MapToDto</c> does
    /// (<see cref="SiteUrlBuilder.BuildContentPath"/>), and is empty when <paramref name="page"/> is null (a
    /// narrow delete race) or <paramref name="urlContext"/> is (no base URL).
    /// </summary>
    protected virtual ContentDto MapToDto(Content content, Page? page, SiteUrlContext? urlContext)
    {
        var dto = ObjectMapper.Map<Content, ContentDto>(content);
        dto.FieldValues = content.FlexFields.ToValueDictionary();
        dto.Url = page != null && urlContext != null
            ? UrlBuilder.BuildContentPath(urlContext, page, content)
            : string.Empty;
        return dto;
    }

    /// <summary>
    /// The URL context, or null when the site has no base URL - no primary domain configured, and no request
    /// to infer one from. The path itself does not depend on the domain, but it goes through the one URL
    /// builder rather than a second composition; and an administration list must not fail because SEO is
    /// not configured yet, so the URL is left empty instead.
    /// </summary>
    protected virtual async Task<SiteUrlContext?> TryCreateUrlContextAsync()
    {
        try
        {
            return await UrlBuilder.CreateContextAsync();
        }
        catch (PrimaryDomainNotConfiguredException)
        {
            return null;
        }
    }
}
