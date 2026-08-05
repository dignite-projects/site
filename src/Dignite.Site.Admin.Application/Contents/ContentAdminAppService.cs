using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Common;
using Dignite.Site.Contents;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Contents;

[Authorize(AdminPermissions.Contents.Default)]
public class ContentAdminAppService : AdminAppService, IContentAdminAppService
{
    protected IContentRepository ContentRepository { get; }

    protected ContentManager ContentManager { get; }

    public ContentAdminAppService(IContentRepository contentRepository, ContentManager contentManager)
    {
        ContentRepository = contentRepository;
        ContentManager = contentManager;
    }

    public virtual async Task<ContentDto> GetAsync(Guid id)
    {
        var content = await ContentRepository.GetAsync(id);
        return MapToDto(content);
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

        return new PagedResultDto<ContentDto>(totalCount, contents.Select(MapToDto).ToList());
    }

    [Authorize(AdminPermissions.Contents.Create)]
    public virtual async Task<ContentDto> CreateAsync(CreateContentDto input)
    {
        var content = await ContentManager.CreateAsync(
            input.ContentTypeId, input.CultureName, input.Slug, input.PublishTime, input.Status,
            input.FieldValues);

        return MapToDto(content);
    }

    [Authorize(AdminPermissions.Contents.Update)]
    public virtual async Task<ContentDto> UpdateAsync(Guid id, UpdateContentDto input)
    {
        var content = await ContentRepository.GetAsync(id);

        content = await ContentManager.UpdateAsync(
            content, input.Slug, input.PublishTime, input.Status, input.FieldValues, input.ContentTypeId);

        return MapToDto(content);
    }

    [Authorize(AdminPermissions.Contents.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Nothing else references a content by FK except its own (cascading) query-index rows.
        await ContentRepository.DeleteAsync(id);
    }

    protected virtual ContentDto MapToDto(Content content)
    {
        var dto = ObjectMapper.Map<Content, ContentDto>(content);
        dto.FieldValues = content.FlexFields.ToValueDictionary();
        return dto;
    }
}
