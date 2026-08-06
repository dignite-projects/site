using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Schema;

/// <summary>
/// Delivers the site's whole content schema in one read (总体设计 §6.2.4).
/// <para>
/// It exists because the discovery sequence is otherwise three round trips and the third is not
/// skippable: <c>ContentTypeField</c> carries only a <c>FieldId</c> plus usage flags, while the field's
/// <c>Name</c> - the key a write has to use - lives in a different table. The way to shorten that
/// sequence is not to make the write tools coarser, it is to deliver the schema in one shot.
/// </para>
/// <para>
/// Read-only, and it composes existing reads rather than adding a second path to anything.
/// </para>
/// </summary>
public interface ISiteSchemaAdminAppService : IApplicationService
{
    Task<SiteSchemaDto> GetAsync();
}
