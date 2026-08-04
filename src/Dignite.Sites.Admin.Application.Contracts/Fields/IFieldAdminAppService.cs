using System;
using System.Threading.Tasks;
using Dignite.Sites.Fields;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Sites.Admin.Fields;

public interface IFieldAdminAppService : IApplicationService
{
    Task<FieldDto> GetAsync(Guid id);

    Task<PagedResultDto<FieldDto>> GetListAsync(GetFieldListInput input);

    Task<FieldDto> CreateAsync(CreateFieldDto input);

    Task<FieldDto> UpdateAsync(Guid id, UpdateFieldDto input);

    /// <summary>
    /// Renames a field definition and moves every content's stored value to the new key. Deliberately
    /// separate from <see cref="UpdateAsync"/> and its own permission - unlike an ordinary property edit,
    /// this is a data migration across the whole content table (总体设计 §2.4; see
    /// <c>FieldManager.RenameAsync</c>).
    /// </summary>
    Task<FieldDto> RenameAsync(Guid id, RenameFieldDto input);

    /// <summary>
    /// Deletes the definition and strips its values from every content's bag. Does not unhook the field
    /// from content types that still reference it - by design, see <c>FieldManager.DeleteAsync</c>.
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>Every field type registered with the FlexFields kernel, for populating a "type" picker.</summary>
    Task<ListResultDto<FieldTypeDto>> GetFieldTypesAsync();
}
