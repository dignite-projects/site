using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Sites.Admin.Permissions;
using Dignite.Sites.Common;
using Dignite.Sites.Fields;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dignite.Sites.Admin.Fields;

[Authorize(AdminPermissions.Fields.Default)]
public class FieldAdminAppService : AdminAppService, IFieldAdminAppService
{
    protected IFieldRepository FieldRepository { get; }

    protected FieldManager FieldManager { get; }

    protected IFieldTypeResolver FieldTypeResolver { get; }

    public FieldAdminAppService(
        IFieldRepository fieldRepository,
        FieldManager fieldManager,
        IFieldTypeResolver fieldTypeResolver)
    {
        FieldRepository = fieldRepository;
        FieldManager = fieldManager;
        FieldTypeResolver = fieldTypeResolver;
    }

    public virtual async Task<FieldDto> GetAsync(Guid id)
    {
        var field = await FieldRepository.GetAsync(id);
        return MapToDto(field);
    }

    public virtual async Task<PagedResultDto<FieldDto>> GetListAsync(GetFieldListInput input)
    {
        var totalCount = await FieldRepository.GetCountAsync(groupId: input.GroupId, filter: input.Filter);

        var fields = await FieldRepository.GetListAsync(
            groupId: input.GroupId, filter: input.Filter, maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount, sorting: input.Sorting);

        return new PagedResultDto<FieldDto>(totalCount, fields.Select(MapToDto).ToList());
    }

    [Authorize(AdminPermissions.Fields.Create)]
    public virtual async Task<FieldDto> CreateAsync(CreateFieldDto input)
    {
        var field = await FieldManager.CreateAsync(
            input.Name, input.DisplayName, input.FieldTypeName, input.Description,
            input.Configuration.ToFieldConfiguration(), input.GroupId);

        return MapToDto(field);
    }

    [Authorize(AdminPermissions.Fields.Update)]
    public virtual async Task<FieldDto> UpdateAsync(Guid id, UpdateFieldDto input)
    {
        var field = await FieldRepository.GetAsync(id);

        field = await FieldManager.UpdateAsync(
            field, input.DisplayName, input.FieldTypeName, input.Description,
            input.Configuration.ToFieldConfiguration(), input.GroupId);

        return MapToDto(field);
    }

    [Authorize(AdminPermissions.Fields.Rename)]
    public virtual async Task<FieldDto> RenameAsync(Guid id, RenameFieldDto input)
    {
        var field = await FieldRepository.GetAsync(id);

        // Mutates and re-saves `field` in place; the migration count is not surfaced to the caller.
        await FieldManager.RenameAsync(field, input.NewName);

        return MapToDto(field);
    }

    [Authorize(AdminPermissions.Fields.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var field = await FieldRepository.GetAsync(id);
        await FieldManager.DeleteAsync(field);
    }

    public virtual Task<ListResultDto<FieldTypeDto>> GetFieldTypesAsync()
    {
        var fieldTypes = FieldTypeResolver.GetAll()
            .Select(fieldType => new FieldTypeDto { Name = fieldType.Name, Indexable = fieldType.IsIndexable() })
            .ToList();

        return Task.FromResult(new ListResultDto<FieldTypeDto>(fieldTypes));
    }

    protected virtual FieldDto MapToDto(Field field)
    {
        var dto = ObjectMapper.Map<Field, FieldDto>(field);
        dto.Configuration = field.Configuration.ToValueDictionary();
        return dto;
    }
}
