using System;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Seo;

public class SiteJsonLd_Tests
{
    [Fact]
    public void A_Single_Node_Should_Get_The_Context_First()
    {
        var json = SiteJsonLd.Serialize(JsonNode.Parse("""{ "@type": "Organization", "name": "Acme" }"""));

        json.ShouldBe("""{"@context":"https://schema.org","@type":"Organization","name":"Acme"}""");
    }

    [Fact]
    public void Several_Nodes_Should_Be_One_Graph()
    {
        var json = SiteJsonLd.Serialize(JsonNode.Parse("""[{ "@type": "Organization" }, { "@type": "WebSite" }]"""));

        json.ShouldBe("""{"@context":"https://schema.org","@graph":[{"@type":"Organization"},{"@type":"WebSite"}]}""");
    }

    /// <summary>GitHub issue #20: text holding &lt;/script&gt; must not end the script it is written in.</summary>
    [Fact]
    public void Markup_Should_Be_Escaped_And_Other_Text_Written_As_It_Is()
    {
        var json = SiteJsonLd.Serialize(JsonNode.Parse("""{ "@type": "Article", "headline": "</script><b>日本語 & more" }"""));

        json.ShouldNotContain("<");
        json.ShouldNotContain(">");
        json.ShouldContain("\\u003C/script\\u003E");
        json.ShouldContain("日本語");
        json.ShouldContain("\\u0026");
    }

    [Fact]
    public void Empty_Values_Should_Be_Left_Out_At_Any_Depth()
    {
        var json = SiteJsonLd.Serialize(JsonNode.Parse(
            """{ "@type": "Person", "name": " ", "jobTitle": null, "sameAs": [], "address": { "streetAddress": "" }, "knowsAbout": ["", "Tea"] }"""));

        json.ShouldBe("""{"@context":"https://schema.org","@type":"Person","knowsAbout":["Tea"]}""");
    }

    [Fact]
    public void An_Empty_List_Should_Write_Nothing()
    {
        SiteJsonLd.Serialize(new JsonArray()).ShouldBeNull();
    }

    [Fact]
    public void A_Node_Without_A_Type_Should_Be_Refused()
    {
        Should.Throw<ArgumentException>(() => SiteJsonLd.Serialize(JsonNode.Parse("""{ "name": "Acme" }""")))
            .Message.ShouldContain("@type");
        Should.Throw<ArgumentException>(() => SiteJsonLd.Serialize(JsonNode.Parse("""[{ "@type": "WebSite" }, "Acme"]""")))
            .Message.ShouldContain("node 2");
        Should.Throw<ArgumentException>(() => SiteJsonLd.Serialize(JsonValue.Create("Acme")));
    }

    [Fact]
    public void Site_Addresses_Should_Be_Made_Absolute_And_Others_Left_Alone()
    {
        var resolve = SiteJsonLd.CreateUrlResolver("https://acme.example/", path => path.Replace("~/", "~/ja/"));

        var json = SiteJsonLd.Serialize(JsonNode.Parse(
            """{ "@type": "Organization", "url": "~/about", "logo": "/api/site-public/files/images/logo.png", "sameAs": ["https://github.com/acme"], "slogan": "/not-a-link" }"""),
            resolve);

        json.ShouldContain("\"url\":\"https://acme.example/ja/about\"");
        json.ShouldContain("\"logo\":\"https://acme.example/api/site-public/files/images/logo.png\"");
        json.ShouldContain("\"sameAs\":[\"https://github.com/acme\"]");
        json.ShouldContain("\"slogan\":\"/not-a-link\"");
    }

    [Fact]
    public void Without_A_Base_Url_Site_Addresses_Should_Stay_Site_Relative()
    {
        var resolve = SiteJsonLd.CreateUrlResolver(null, path => path);

        resolve("~/about").ShouldBe("/about");
    }
}
