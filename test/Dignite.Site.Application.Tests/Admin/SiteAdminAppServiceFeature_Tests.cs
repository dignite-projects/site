using System;
using System.Linq;
using System.Reflection;
using Dignite.Site.Features;
using Shouldly;
using Volo.Abp.Application.Services;
using Volo.Abp.Features;
using Xunit;

namespace Dignite.Site.Admin;

/// <summary>
/// The gate lives on <see cref="SiteAdminAppService"/> and reaches each Admin service by inheritance, so a
/// new service that skips the base class would be silently ungated. This is the check that catches it - the
/// same job <c>Dignite.Site.Mcp.Tests</c>' reflection test does for the MCP/DTO contract.
/// </summary>
public class SiteAdminAppServiceFeature_Tests
{
    [Fact]
    public void Every_Admin_Application_Service_Should_Require_The_Site_Feature()
    {
        var services = typeof(SiteAdminApplicationModule).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                           && typeof(IApplicationService).IsAssignableFrom(type))
            .ToList();

        services.ShouldNotBeEmpty("the scan should find the Admin application services");

        var ungated = services
            .Where(type => !RequiresSiteFeature(type))
            .Select(type => type.FullName)
            .ToList();

        ungated.ShouldBeEmpty(
            $"every Admin application service must derive from {nameof(SiteAdminAppService)} " +
            $"(or carry [RequiresFeature(\"{SiteFeatures.Enable}\")] itself)");
    }

    private static bool RequiresSiteFeature(Type type)
    {
        return type
            .GetCustomAttributes<RequiresFeatureAttribute>(inherit: true)
            .Any(attribute => attribute.Features.Contains(SiteFeatures.Enable));
    }
}
