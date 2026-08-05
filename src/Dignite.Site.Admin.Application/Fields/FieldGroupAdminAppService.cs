using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Fields;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Fields;

/// <summary>
/// <c>FieldGroup</c> has no domain manager - it carries no invariant beyond name uniqueness, which this
/// service checks directly against <see cref="IFieldGroupRepository.NameExistsAsync"/> before writing,
/// the same repository-level check every other entity's manager runs internally.
/// </summary>
[Authorize(AdminPermissions.FieldGroups.Default)]
public class FieldGroupAdminAppService : AdminAppService, IFieldGroupAdminAppService
{
    protected IFieldGroupRepository FieldGroupRepository { get; }

    public FieldGroupAdminAppService(IFieldGroupRepository fieldGroupRepository)
    {
        FieldGroupRepository = fieldGroupRepository;
    }

    public virtual async Task<FieldGroupDto> GetAsync(Guid id)
    {
        var fieldGroup = await FieldGroupRepository.GetAsync(id);
        return ObjectMapper.Map<FieldGroup, FieldGroupDto>(fieldGroup);
    }

    public virtual async Task<ListResultDto<FieldGroupDto>> GetListAsync()
    {
        var fieldGroups = await FieldGroupRepository.GetListAsync();
        return new ListResultDto<FieldGroupDto>(
            fieldGroups.Select(fg => ObjectMapper.Map<FieldGroup, FieldGroupDto>(fg)).ToList());
    }

    [Authorize(AdminPermissions.FieldGroups.Create)]
    public virtual async Task<FieldGroupDto> CreateAsync(CreateFieldGroupDto input)
    {
        await CheckNameAsync(input.Name, null);

        var fieldGroup = await FieldGroupRepository.InsertAsync(
            new FieldGroup(GuidGenerator.Create(), input.Name, input.Order, CurrentTenant.Id));

        return ObjectMapper.Map<FieldGroup, FieldGroupDto>(fieldGroup);
    }

    [Authorize(AdminPermissions.FieldGroups.Update)]
    public virtual async Task<FieldGroupDto> UpdateAsync(Guid id, UpdateFieldGroupDto input)
    {
        var fieldGroup = await FieldGroupRepository.GetAsync(id);

        if (!string.Equals(fieldGroup.Name, input.Name, StringComparison.Ordinal))
        {
            await CheckNameAsync(input.Name, id);
            fieldGroup.SetName(input.Name);
        }

        fieldGroup.SetOrder(input.Order);

        fieldGroup = await FieldGroupRepository.UpdateAsync(fieldGroup);

        return ObjectMapper.Map<FieldGroup, FieldGroupDto>(fieldGroup);
    }

    [Authorize(AdminPermissions.FieldGroups.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Safe unconditionally - the FK from Field to FieldGroup is SetNull, so this only clears GroupId
        // on the fields that were filed under it.
        await FieldGroupRepository.DeleteAsync(id);
    }

    protected virtual async Task CheckNameAsync(string name, Guid? excludedId)
    {
        if (await FieldGroupRepository.NameExistsAsync(name, excludedId))
        {
            throw new UserFriendlyException(L["FieldGroupNameAlreadyExists", name]);
        }
    }
}
