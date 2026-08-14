using System;
using System.Threading.Tasks;
using Dignite.Site.Pages;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Pages;

/// <summary>
/// Read-only, and only ever active pages - an inactive one is not routable and must not be discoverable
/// through the public surface either (总体设计 §2.2).
/// </summary>
public interface IPagePublicAppService : IApplicationService
{
    Task<PageDto> GetAsync(Guid id);

    /// <summary>Step 1 of route resolution (总体设计 §3.4).</summary>
    Task<PageDto> GetByRouteAsync(string route);

    /// <summary>
    /// Null - never a thrown not-found - when no active page has this name, so a caller like
    /// <c>ContentListTagHelper</c>'s <c>PageName</c> fallback can render a friendly message instead of
    /// crashing the whole page over an unresolved reference to a page that does not (or no longer) exist.
    /// </summary>
    Task<PageDto?> FindByNameAsync(string name);

    Task<ListResultDto<PageDto>> GetListAsync(GetPageListInput input);
}
