using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Site.ContentTypes;

public interface IContentTypeRepository : IBasicRepository<ContentType, Guid>
{
    Task<List<ContentType>> GetListByPageAsync(Guid pageId, CancellationToken cancellationToken = default);

    Task<ContentType?> FindByNameAsync(Guid pageId, string name, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(Guid pageId, string name, Guid? excludedId = null, CancellationToken cancellationToken = default);
}
