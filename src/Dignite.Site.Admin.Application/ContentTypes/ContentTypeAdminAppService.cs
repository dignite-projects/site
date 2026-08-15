using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Common;
using Dignite.Site.Contents;
using Dignite.Site.ContentTypes;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.ContentTypes;

[Authorize(SiteAdminPermissions.ContentTypes.Default)]
public class ContentTypeAdminAppService : SiteAdminAppService, IContentTypeAdminAppService
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

    public virtual async Task<ContentTypeDto?> FindByNameAsync(Guid pageId, string name)
    {
        var contentType = await ContentTypeRepository.FindByNameAsync(pageId, name);
        return contentType == null ? null : MapToDto(contentType);
    }

    public virtual async Task<ListResultDto<ContentTypeDto>> GetListByPageAsync(Guid pageId)
    {
        var contentTypes = await ContentTypeRepository.GetListByPageAsync(pageId);
        return new ListResultDto<ContentTypeDto>(contentTypes.Select(MapToDto).ToList());
    }

    public virtual async Task<ListResultDto<ContentTypeDto>> GetListAsync()
    {
        var contentTypes = await ContentTypeRepository.GetListAsync();
        return new ListResultDto<ContentTypeDto>(contentTypes.Select(MapToDto).ToList());
    }

    [Authorize(SiteAdminPermissions.ContentTypes.Create)]
    public virtual async Task<ContentTypeDto> CreateAsync(CreateContentTypeDto input)
    {
        var contentType = await ContentTypeManager.CreateAsync(
            input.PageId, input.Name, input.DisplayName, input.Description, input.Fields?.ToEntityList());

        return MapToDto(contentType);
    }

    [Authorize(SiteAdminPermissions.ContentTypes.Update)]
    public virtual async Task<ContentTypeDto> UpdateAsync(Guid id, UpdateContentTypeDto input)
    {
        var contentType = await ContentTypeRepository.GetAsync(id);

        contentType = await ContentTypeManager.UpdateAsync(
            contentType, input.Name, input.DisplayName, input.Description, input.Fields?.ToEntityList());

        return MapToDto(contentType);
    }

    [Authorize(SiteAdminPermissions.ContentTypes.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // This check is the only guard, not a friendlier duplicate of one the database also enforces.
        // The FK from Content to ContentType is declared Restrict, but ContentType is soft-deleted - its
        // "delete" is an UPDATE, and a declared FK behavior fires only on an actual DELETE statement, so
        // the database never gets a chance to refuse this (same reason PageManager.DeleteAsync's cascade
        // has to run explicitly rather than relying on ON DELETE CASCADE). Without this check, a content
        // type with live contents would soft-delete successfully, leaving those contents pointing at a
        // type no name-addressed surface can reach any more.
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
