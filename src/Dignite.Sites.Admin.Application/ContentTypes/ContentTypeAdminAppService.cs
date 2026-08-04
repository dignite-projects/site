using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Sites.Admin.Permissions;
using Dignite.Sites.Common;
using Dignite.Sites.Contents;
using Dignite.Sites.ContentTypes;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Sites.Admin.ContentTypes;

[Authorize(AdminPermissions.ContentTypes.Default)]
public class ContentTypeAdminAppService : AdminAppService, IContentTypeAdminAppService
{
    protected IContentTypeRepository ContentTypeRepository { get; }

    protected IContentRepository ContentRepository { get; }

    protected ContentTypeManager ContentTypeManager { get; }

    public ContentTypeAdminAppService(
        IContentTypeRepository contentTypeRepository,
        IContentRepository contentRepository,
        ContentTypeManager contentTypeManager)
    {
        ContentTypeRepository = contentTypeRepository;
        ContentRepository = contentRepository;
        ContentTypeManager = contentTypeManager;
    }

    public virtual async Task<ContentTypeDto> GetAsync(Guid id)
    {
        var contentType = await ContentTypeRepository.GetAsync(id);
        return MapToDto(contentType);
    }

    public virtual async Task<ListResultDto<ContentTypeDto>> GetListByPageAsync(Guid pageId)
    {
        var contentTypes = await ContentTypeRepository.GetListByPageAsync(pageId);
        return new ListResultDto<ContentTypeDto>(contentTypes.Select(MapToDto).ToList());
    }

    [Authorize(AdminPermissions.ContentTypes.Create)]
    public virtual async Task<ContentTypeDto> CreateAsync(CreateContentTypeDto input)
    {
        var contentType = await ContentTypeManager.CreateAsync(
            input.PageId, input.Name, input.DisplayName, input.Description, input.Fields?.ToEntityList());

        return MapToDto(contentType);
    }

    [Authorize(AdminPermissions.ContentTypes.Update)]
    public virtual async Task<ContentTypeDto> UpdateAsync(Guid id, UpdateContentTypeDto input)
    {
        var contentType = await ContentTypeRepository.GetAsync(id);

        contentType = await ContentTypeManager.UpdateAsync(
            contentType, input.Name, input.DisplayName, input.Description, input.Fields?.ToEntityList());

        return MapToDto(contentType);
    }

    [Authorize(AdminPermissions.ContentTypes.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // The FK from Content to ContentType is Restrict, so the database would refuse this anyway; the
        // check here just turns that into a clear message instead of a raw constraint-violation error.
        if (await ContentRepository.AnyByContentTypeAsync(id))
        {
            throw new UserFriendlyException(L["ContentTypeStillHasContents"]);
        }

        await ContentTypeRepository.DeleteAsync(id);
    }

    protected virtual ContentTypeDto MapToDto(ContentType contentType)
    {
        var dto = ObjectMapper.Map<ContentType, ContentTypeDto>(contentType);
        dto.Fields = contentType.Fields.ToDtoList();
        return dto;
    }
}
