using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Common;
using Dignite.Site.ContentTypes;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.ContentTypes;

public class ContentTypePublicAppService : PublicAppService, IContentTypePublicAppService
{
    protected IContentTypeRepository ContentTypeRepository { get; }

    public ContentTypePublicAppService(IContentTypeRepository contentTypeRepository)
    {
        ContentTypeRepository = contentTypeRepository;
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

    protected virtual ContentTypeDto MapToDto(ContentType contentType)
    {
        var dto = ObjectMapper.Map<ContentType, ContentTypeDto>(contentType);
        dto.Fields = contentType.Fields.ToDtoList();
        return dto;
    }
}
