using System;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Fields;

/// <summary>
/// Admin-only: a field group is purely an organizational filing over the library, with no runtime
/// meaning, so it has no Public counterpart (总体设计 §2.3).
/// </summary>
public interface IFieldGroupAdminAppService : IApplicationService
{
    Task<FieldGroupDto> GetAsync(Guid id);

    Task<ListResultDto<FieldGroupDto>> GetListAsync();

    Task<FieldGroupDto> CreateAsync(CreateFieldGroupDto input);

    Task<FieldGroupDto> UpdateAsync(Guid id, UpdateFieldGroupDto input);

    /// <summary>Safe unconditionally - deleting a group only clears its fields' <c>GroupId</c> (database <c>SetNull</c>).</summary>
    Task DeleteAsync(Guid id);
}
