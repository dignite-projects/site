using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.FlexFields.Site;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Common;
using Dignite.Site.Fields;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Fields;

[Authorize(SiteAdminPermissions.Fields.Default)]
public class FieldAdminAppService : SiteAdminAppService, IFieldAdminAppService
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

    public virtual async Task<FieldDto?> FindByNameAsync(string name)
    {
        var field = await FieldRepository.FindByNameAsync(name);
        return field == null ? null : MapToDto(field);
    }

    public virtual async Task<ListResultDto<FieldDto>> GetListAsync(GetFieldListInput input)
    {
        var fields = await FieldRepository.GetListAsync(filter: input.Filter);

        return new ListResultDto<FieldDto>(fields.Select(MapToDto).ToList());
    }

    [Authorize(SiteAdminPermissions.Fields.Create)]
    public virtual async Task<FieldDto> CreateAsync(CreateFieldDto input)
    {
        var field = await FieldManager.CreateAsync(
            input.Name, input.DisplayName, input.FieldTypeName, input.Description,
            input.Configuration.ToFieldConfiguration(), input.GroupName);

        return MapToDto(field);
    }

    [Authorize(SiteAdminPermissions.Fields.Update)]
    public virtual async Task<FieldDto> UpdateAsync(Guid id, UpdateFieldDto input)
    {
        var field = await FieldRepository.GetAsync(id);

        field = await FieldManager.UpdateAsync(
            field, input.DisplayName, input.FieldTypeName, input.Description,
            input.Configuration.ToFieldConfiguration(), input.GroupName);

        return MapToDto(field);
    }

    [Authorize(SiteAdminPermissions.Fields.Rename)]
    public virtual async Task<FieldDto> RenameAsync(Guid id, RenameFieldDto input)
    {
        var field = await FieldRepository.GetAsync(id);

        // Mutates and re-saves `field` in place; the migration count is not surfaced to the caller.
        await FieldManager.RenameAsync(field, input.NewName);

        return MapToDto(field);
    }

    [Authorize(SiteAdminPermissions.Fields.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var field = await FieldRepository.GetAsync(id);
        await FieldManager.DeleteAsync(field);
    }

    public virtual Task<ListResultDto<FieldTypeDto>> GetFieldTypesAsync()
    {
        var fieldTypes = FieldTypeResolver.GetAll()
            .Select(fieldType => new FieldTypeDto
            {
                Name = fieldType.Name,
                Indexable = fieldType.IsIndexable(),
                Composite = fieldType is ICompositeFieldType,
            })
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
