using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Common;
using Dignite.Site.ContentTypes;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.ContentTypes;

[Authorize(SiteAdminPermissions.ContentTypes.Default)]
public class ContentTypeAdminAppService : SiteAdminAppService, IContentTypeAdminAppService
{
    protected IContentTypeRepository ContentTypeRepository { get; }

    protected ContentTypeManager ContentTypeManager { get; }

    public ContentTypeAdminAppService(
        IContentTypeRepository contentTypeRepository,
        ContentTypeManager contentTypeManager)
    {
        ContentTypeRepository = contentTypeRepository;
        ContentTypeManager = contentTypeManager;
    }

    public virtual async Task<ContentTypeDto> GetAsync(Guid id)
    {
        var contentType = await ContentTypeRepository.GetAsync(id);
        return ObjectMapper.Map<ContentType, ContentTypeDto>(contentType);
    }

    public virtual async Task<ContentTypeDto?> FindByNameAsync(Guid pageId, string name)
    {
        var contentType = await ContentTypeRepository.FindByNameAsync(pageId, name);
        return contentType == null ? null : ObjectMapper.Map<ContentType, ContentTypeDto>(contentType);
    }

    public virtual async Task<ListResultDto<ContentTypeDto>> GetListByPageAsync(Guid pageId)
    {
        var contentTypes = await ContentTypeRepository.GetListByPageAsync(pageId);
        return new ListResultDto<ContentTypeDto>(contentTypes.Select(ObjectMapper.Map<ContentType, ContentTypeDto>).ToList());
    }

    public virtual async Task<ListResultDto<ContentTypeDto>> GetListAsync()
    {
        var contentTypes = await ContentTypeRepository.GetListAsync();
        return new ListResultDto<ContentTypeDto>(contentTypes.Select(ObjectMapper.Map<ContentType, ContentTypeDto>).ToList());
    }

    [Authorize(SiteAdminPermissions.ContentTypes.Create)]
    public virtual async Task<ContentTypeDto> CreateAsync(CreateContentTypeDto input)
    {
        var contentType = await ContentTypeManager.CreateAsync(
            input.PageId, input.Name, input.DisplayName, input.Description, input.Fields?.ToEntityList());

        return ObjectMapper.Map<ContentType, ContentTypeDto>(contentType);
    }

    [Authorize(SiteAdminPermissions.ContentTypes.Update)]
    public virtual async Task<ContentTypeDto> UpdateAsync(Guid id, UpdateContentTypeDto input)
    {
        var contentType = await ContentTypeRepository.GetAsync(id);

        contentType = await ContentTypeManager.UpdateAsync(
            contentType, input.Name, input.DisplayName, input.Description, input.Fields?.ToEntityList());

        return ObjectMapper.Map<ContentType, ContentTypeDto>(contentType);
    }

    [Authorize(SiteAdminPermissions.ContentTypes.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await ContentTypeManager.DeleteAsync(await ContentTypeRepository.GetAsync(id));
    }
}
