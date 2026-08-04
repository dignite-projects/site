using System;
using System.Threading.Tasks;
using Dignite.Sites.Pages;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Sites.Public.Pages;

/// <summary>
/// Read-only, and only ever active pages - an inactive one is not routable and must not be discoverable
/// through the public surface either (总体设计 §2.2).
/// </summary>
public interface IPagePublicAppService : IApplicationService
{
    Task<PageDto> GetAsync(Guid id);

    /// <summary>Step 1 of route resolution (总体设计 §3.4).</summary>
    Task<PageDto> GetByRouteAsync(string route);

    Task<PagedResultDto<PageDto>> GetListAsync(GetPageListInput input);
}
