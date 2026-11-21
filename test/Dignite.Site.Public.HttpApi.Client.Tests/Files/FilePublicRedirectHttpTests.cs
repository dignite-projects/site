using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Volo.Abp;
using Volo.Abp.AspNetCore.TestBase;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Site.Public.Files;

/// <summary>
/// The address Site served its files at before 3.0 answers 301 with Blob Management's public address of the same file,
/// the size passed on as <c>SiteFileUrl.Sized</c> writes it and the format dropped.
/// </summary>
#pragma warning disable CS0618 // AbpAspNetCoreIntegratedTestBase, as the other HTTP tests here use it
public class FilePublicRedirectHttpTests : AbpAspNetCoreIntegratedTestBase<FileRedirectServerModule>
{
    [Theory]
    [InlineData("/api/site-public/files/images/abc.webp", "/blobs/images/abc.webp")]
    [InlineData("/api/site-public/files/images/abc.webp?Width=300", "/blobs/images/abc.webp?width=384")]
    [InlineData("/api/site-public/files/images/abc.webp?Width=1200&Height=630&Format=webp", "/blobs/images/abc.webp?width=1200&height=630")]
    [InlineData("/api/site-public/files/attachments/2026/a.pdf", "/blobs/attachments/2026/a.pdf")]
    [InlineData("/api/site-public/files/download/attachments/a.pdf?fileName=x.pdf", "/blobs/attachments/a.pdf")]
    public async Task Should_Redirect_To_Blob_Managements_Address(string earlier, string expected)
    {
        using var client = Server.CreateClient();

        using var response = await client.GetAsync(earlier);

        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.OriginalString.ShouldBe(expected);
    }

    [Fact]
    public async Task Should_Redirect_A_Head_Request_Too()
    {
        using var client = Server.CreateClient();

        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/site-public/files/images/abc.webp"));

        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.OriginalString.ShouldBe("/blobs/images/abc.webp");
    }
}

#pragma warning restore CS0618

/// <summary>The real <see cref="SitePublicHttpApiModule"/> pipeline; the redirect needs no service behind it.</summary>
[DependsOn(
    typeof(SitePublicHttpApiModule),
    typeof(AbpAspNetCoreTestBaseModule),
    typeof(AbpAutofacModule))]
public class FileRedirectServerModule : AbpModule
{
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseRouting();
        app.UseConfiguredEndpoints();
    }
}
