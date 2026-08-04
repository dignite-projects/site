using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Sites.Fields;

public interface IFieldGroupRepository : IBasicRepository<FieldGroup, Guid>
{
    Task<List<FieldGroup>> GetListAsync(bool includeDetails = false, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludedId = null, CancellationToken cancellationToken = default);
}
