using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Contents;
using Dignite.Site.ContentTypes;
using Dignite.Site.Pages;
using Dignite.Site.Seo;
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

    protected SiteLanguageProvider LanguageProvider { get; }

    public ContentAdminAppService(
        IContentRepository contentRepository,
        ContentManager contentManager,
        IPageRepository pageRepository,
        IContentTypeRepository contentTypeRepository,
        SiteLanguageProvider languageProvider)
    {
        ContentRepository = contentRepository;
        ContentManager = contentManager;
        PageRepository = pageRepository;
        ContentTypeRepository = contentTypeRepository;
        LanguageProvider = languageProvider;
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

        var content = await ContentRepository.FindBySlugAsync(pageId, cultureName, slug);

        return content == null ? null : await MapToDtoAsync(content);
    }

    public virtual async Task<string> GetPathAsync(GetContentPathInput input)
    {
        var page = await PageRepository.GetAsync(input.PageId, includeDetails: false);

        // Never saved: the content as the editor has it, for the page to build the address from as it builds a saved one's.
        var content = new Content(Guid.Empty, page.Id, Guid.Empty, input.CultureName, input.Slug, input.PublishTime);
        foreach (var (name, value) in input.FieldValues ?? new Dictionary<string, object?>())
        {
            if (value != null)
            {
                content.SetField(name, value);
            }
        }

        var languages = await LanguageProvider.GetEnabledLanguagesAsync();
        return new SiteLanguages(languages[0], languages).ApplyCulturePrefix(page.PreviewContentPath(content), content.CultureName);
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
                dto.PageName = pageNames.GetValueOrDefault(content.PageId) ?? string.Empty;
                dto.ContentTypeName = contentTypeNames.GetValueOrDefault(content.ContentTypeId) ?? string.Empty;
                return dto;
            })
            .ToList();
    }
}
