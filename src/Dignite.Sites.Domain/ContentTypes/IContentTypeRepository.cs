using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Sites.ContentTypes;

public interface IContentTypeRepository : IBasicRepository<ContentType, Guid>
{
    Task<List<ContentType>> GetListByPageAsync(Guid pageId, CancellationToken cancellationToken = default);

    Task<ContentType?> FindByNameAsync(Guid pageId, string name, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(Guid pageId, string name, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<List<ContentType>> GetListAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every content type that pulls in <paramref name="fieldId"/> - what a field definition has to be
    /// checked against before it is deleted, and the list of types affected by a rename.
    /// </summary>
    Task<List<ContentType>> GetListByFieldAsync(Guid fieldId, CancellationToken cancellationToken = default);
}
