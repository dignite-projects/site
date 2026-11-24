using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Fields;

public class FieldPublicAppService : SitePublicAppService, IFieldPublicAppService
{
    protected IFieldRepository FieldRepository { get; }

    public FieldPublicAppService(IFieldRepository fieldRepository)
    {
        FieldRepository = fieldRepository;
    }

    public virtual async Task<FieldDto> GetAsync(Guid id)
    {
        var field = await FieldRepository.GetAsync(id);
        return ObjectMapper.Map<Field, FieldDto>(field);
    }

    public virtual async Task<ListResultDto<FieldDto>> GetListAsync(IEnumerable<Guid> ids)
    {
        var fields = await FieldRepository.GetListAsync(ids);
        return new ListResultDto<FieldDto>(fields.Select(ObjectMapper.Map<Field, FieldDto>).ToList());
    }
}
