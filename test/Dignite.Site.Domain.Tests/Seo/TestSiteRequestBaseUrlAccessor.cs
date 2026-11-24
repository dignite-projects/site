using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Seo;

/// <summary>Stands in for the request a web host would be serving; none unless a test sets one.</summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ISiteRequestBaseUrlAccessor), typeof(TestSiteRequestBaseUrlAccessor))]
public class TestSiteRequestBaseUrlAccessor : ISiteRequestBaseUrlAccessor, ISingletonDependency
{
    public string? BaseUrl { get; set; }

    public string? GetBaseUrl() => BaseUrl;
}
