using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;

namespace Dignite.Site.Fields;

/// <summary>
/// Repository for the field library.
/// <para>
/// Extends FlexFields' <c>IFlexFieldRepository&lt;Field&gt;</c>, which already supplies the three
/// operations the kernel's own rename flow needs (<c>FindByNameAsync</c>, <c>GetListAsync(ids)</c>,
/// <c>NameExistsAsync</c>). Note that the kernel never calls this itself - it walks the host-entity axis
/// through <c>IFlexFieldProvider&lt;Content&gt;</c> instead; this is the definition axis, for Site' own
/// use.
/// </para>
/// </summary>
public interface IFieldRepository : IFlexFieldRepository<Field>
{
    Task<List<Field>> GetListAsync(
        string? filter = null,
        CancellationToken cancellationToken = default);
}
