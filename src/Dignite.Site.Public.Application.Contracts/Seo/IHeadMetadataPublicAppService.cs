using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Head metadata for a resolved route (总体设计 §5.3, §5.4, §5.5, §5.9 - GitHub issues #13, #16, #17,
/// #20). Always resolves against published content only, the same "no <c>includeUnpublished</c> override
/// on the public surface" rule <c>IRoutingPublicAppService</c> follows.
/// </summary>
public interface IHeadMetadataPublicAppService : IApplicationService
{
    /// <summary>Null when <paramref name="input"/>'s path does not resolve to a page or content.</summary>
    Task<HeadMetadataDto?> ResolveAsync(ResolveHeadMetadataInput input);
}
