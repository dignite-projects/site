using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// Opening a preview link: a good token becomes the preview cookie, any token is taken out of the address, and a bad
/// one sets nothing.
/// </summary>
public class SitePreviewResourceFilter_Tests
{
    private static readonly Guid TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ReleaseId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private sealed class Fixture
    {
        public Fixture(string path = "/ja/about", string? query = null, string pathBase = "", string method = "GET")
        {
            Host = new SiteLanguageTestHost("en,ja", path, pathBase);
            Host.HttpContext.Request.Method = method;
            if (query != null)
            {
                Host.HttpContext.Request.QueryString = new QueryString(query);
            }

            Preview = Host.Preview;
            Filter = new SitePreviewResourceFilter(Preview.Protector, Host.CurrentTenant, Preview.Options);
        }

        public SiteLanguageTestHost Host { get; }

        public SitePreviewTestContext Preview { get; }

        public SitePreviewResourceFilter Filter { get; }

        public bool NextCalled { get; private set; }

        public HttpResponse Response => Host.HttpContext.Response;

        public async Task<IActionResult?> RunAsync()
        {
            var actionContext = new ActionContext(Host.HttpContext, new RouteData(), new ActionDescriptor());
            var context = new ResourceExecutingContext(actionContext, new List<IFilterMetadata>(), new List<IValueProviderFactory>());

            await Filter.OnResourceExecutionAsync(context, () =>
            {
                NextCalled = true;
                return Task.FromResult(new ResourceExecutedContext(actionContext, new List<IFilterMetadata>()));
            });

            return context.Result;
        }

        public IList<SetCookieHeaderValue> SetCookies =>
            SetCookieHeaderValue.ParseList(Response.Headers.SetCookie.Select(value => value!).ToList());
    }

    private static string Query(string token, string? before = null, string? after = null)
    {
        return "?" + (before == null ? "" : before + "&") + SitePreviewConsts.QueryKey + "=" + token +
               (after == null ? "" : "&" + after);
    }

    [Fact]
    public async Task A_Valid_Token_Should_Become_The_Cookie_And_Leave_The_Address()
    {
        var fixture = new Fixture();
        var token = fixture.Preview.Token(ReleaseId);
        fixture.Host.HttpContext.Request.QueryString = new QueryString(Query(token, "x=1", "y=2"));

        var result = await fixture.RunAsync();

        fixture.NextCalled.ShouldBeFalse();
        var redirect = result.ShouldBeOfType<RedirectResult>();
        redirect.Url.ShouldBe("/ja/about?x=1&y=2");
        redirect.Permanent.ShouldBeFalse();

        var cookie = fixture.SetCookies.ShouldHaveSingleItem();
        cookie.Name.ToString().ShouldBe(SitePreviewConsts.CookieName);
        cookie.HttpOnly.ShouldBeTrue();
        cookie.SameSite.ShouldBe(Microsoft.Net.Http.Headers.SameSiteMode.Lax);
        cookie.Secure.ShouldBeFalse();
        cookie.Path.ToString().ShouldBe("/");
        cookie.Expires!.Value.ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(119), DateTimeOffset.UtcNow.AddMinutes(121));

        // A fresh value, not the link's token, naming the same release.
        cookie.Value.ToString().ShouldNotBe(token);
        fixture.Preview.Protector.TryUnprotect(cookie.Value.ToString(), out var ticket).ShouldBeTrue();
        ticket!.ReleaseId.ShouldBe(ReleaseId);

        fixture.Response.Headers["X-Robots-Tag"].ToString().ShouldBe("noindex");
        fixture.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
    }

    [Fact]
    public async Task The_Cookie_And_The_Redirect_Should_Keep_The_Base_Path()
    {
        var fixture = new Fixture("/about", pathBase: "/site");
        fixture.Host.HttpContext.Request.IsHttps = true;
        fixture.Host.HttpContext.Request.QueryString = new QueryString(Query(fixture.Preview.Token()));

        var result = await fixture.RunAsync();

        result.ShouldBeOfType<RedirectResult>().Url.ShouldBe("/site/about");
        var cookie = fixture.SetCookies.ShouldHaveSingleItem();
        cookie.Path.ToString().ShouldBe("/site");
        cookie.Secure.ShouldBeTrue();
    }

    [Fact]
    public async Task A_Token_Of_The_Current_Tenant_Should_Become_The_Cookie()
    {
        var fixture = new Fixture();
        fixture.Host.HttpContext.Request.QueryString = new QueryString(Query(fixture.Preview.Token(tenantId: TenantId)));

        using (fixture.Host.CurrentTenant.Change(TenantId))
        {
            await fixture.RunAsync();
        }

        fixture.SetCookies.ShouldHaveSingleItem();
    }

    public static IEnumerable<object[]> BadTokens()
    {
        // Protected with keys of its own, which no fixture has.
        var foreign = new SiteLanguageTestHost("en").Preview.Token();

        yield return new object[] { "garbage" };
        yield return new object[] { foreign };
        yield return new object[] { foreign[..^10] };
    }

    /// <summary>
    /// Garbage, truncated, protected with keys this site does not have: taken out of the address all the same, but
    /// nothing is set, and the visitor sees the live site.
    /// </summary>
    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task A_Token_That_Does_Not_Verify_Should_Set_Nothing(string token)
    {
        var fixture = new Fixture(query: Query(token, "x=1"));

        var result = await fixture.RunAsync();

        result.ShouldBeOfType<RedirectResult>().Url.ShouldBe("/ja/about?x=1");
        fixture.SetCookies.ShouldBeEmpty();
        fixture.NextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task An_Expired_Token_Should_Set_Nothing()
    {
        var fixture = new Fixture();
        fixture.Host.HttpContext.Request.QueryString =
            new QueryString(Query(fixture.Preview.Token(lifetime: TimeSpan.FromSeconds(-1))));

        var result = await fixture.RunAsync();

        result.ShouldBeOfType<RedirectResult>().Url.ShouldBe("/ja/about");
        fixture.SetCookies.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Tampered_Token_Should_Set_Nothing()
    {
        var fixture = new Fixture();
        var characters = fixture.Preview.Token().ToCharArray();
        var middle = characters.Length / 2;
        characters[middle] = characters[middle] == 'A' ? 'B' : 'A';
        fixture.Host.HttpContext.Request.QueryString = new QueryString(Query(new string(characters)));

        var result = await fixture.RunAsync();

        result.ShouldBeOfType<RedirectResult>().Url.ShouldBe("/ja/about");
        fixture.SetCookies.ShouldBeEmpty();
    }

    [Fact]
    public async Task Another_Tenants_Token_Should_Set_Nothing()
    {
        var fixture = new Fixture();
        fixture.Host.HttpContext.Request.QueryString = new QueryString(Query(fixture.Preview.Token(tenantId: TenantId)));

        // The request is the host's.
        var result = await fixture.RunAsync();

        result.ShouldBeOfType<RedirectResult>().Url.ShouldBe("/ja/about");
        fixture.SetCookies.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("?x=1")]
    [InlineData("?site-preview=")]
    public async Task A_Request_Without_A_Token_Should_Go_On(string? query)
    {
        var fixture = new Fixture(query: query);

        var result = await fixture.RunAsync();

        fixture.NextCalled.ShouldBeTrue();
        result.ShouldBeNull();
        fixture.SetCookies.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Post_Should_Go_On()
    {
        var fixture = new Fixture(method: "POST");
        fixture.Host.HttpContext.Request.QueryString = new QueryString(Query(fixture.Preview.Token()));

        await fixture.RunAsync();

        fixture.NextCalled.ShouldBeTrue();
        fixture.SetCookies.ShouldBeEmpty();
    }

    /// <summary><c>//evil.example</c> as a redirect target would leave the site.</summary>
    [Fact]
    public async Task The_Redirect_Should_Never_Leave_The_Origin()
    {
        var fixture = new Fixture("//evil.example/x", Query("garbage"));

        var result = await fixture.RunAsync();

        result.ShouldBeOfType<RedirectResult>().Url.ShouldBe("/evil.example/x");
    }
}
