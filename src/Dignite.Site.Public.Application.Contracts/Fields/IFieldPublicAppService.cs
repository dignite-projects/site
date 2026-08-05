using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Fields;

/// <summary>
/// Read-only, and scoped to resolving specific ids a content type already named - not a browsable field
/// library (that stays Admin-only; a field group has no runtime meaning at all, see
/// <c>IFieldGroupAdminAppService</c>).
/// </summary>
public interface IFieldPublicAppService : IApplicationService
{
    Task<FieldDto> GetAsync(Guid id);

    Task<ListResultDto<FieldDto>> GetListAsync(IEnumerable<Guid> ids);
}
