namespace Dignite.Site.Public;

public class SitePublicRemoteServiceConsts
{
    public const string RemoteServiceName = "SitePublic";

    /// <summary>
    /// The remote service the Public integration services are called with
    /// (<c>RemoteServices:SitePublicIntegration:BaseUrl</c>): the application that holds the data, reached directly, since
    /// an API gateway does not forward <c>/integration-api/</c>.
    /// </summary>
    public const string IntegrationRemoteServiceName = "SitePublicIntegration";

    public const string ModuleName = "site-public";
}
