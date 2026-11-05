using System;
using System.Linq;
using System.Text.Json;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Seo;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dignite.Site.Public.TagHelpers;

/// <summary>
/// Writes one resolved route's <c>&lt;head&gt;</c> tags from its <see cref="HeadMetadataDto"/> - description,
/// canonical, robots, Open Graph, Twitter card, hreflang and x-default - so a host's layout does not each
/// spell them out by hand, e.g.
/// <c>&lt;site-head-metadata metadata="@Model.HeadMetadata" title="@ViewBag.Title" /&gt;</c> inside the
/// section the host's layout renders into <c>&lt;head&gt;</c>.
/// <para>
/// The tags that come from the route, and what the template adds to them: a share image (<see cref="Image"/>), the
/// <c>og:type</c> (<see cref="OgType"/>) and structured data (<see cref="Schema"/>). <c>&lt;title&gt;</c>, charset and
/// viewport belong to the host's own layout, which knows its branding; the shape of the structured data is the
/// template's own choice as well (总体设计 §5.4) - Site only writes it correctly (<see cref="SiteJsonLd"/>).
/// </para>
/// </summary>
[HtmlTargetElement("site-head-metadata", TagStructure = TagStructure.WithoutEndTag)]
public class SiteHeadMetadataTagHelper : TagHelper
{
    /// <summary>The route's head metadata. Null - no route matched - writes only a <see cref="FallbackDescription"/>.</summary>
    public HeadMetadataDto? Metadata { get; set; }

    /// <summary>
    /// The page's own title for <c>og:title</c>, when the template has one - e.g. an archive titled with its
    /// page number. Falls back to <see cref="HeadMetadataDto.MetaTitle"/>.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// A template's own description, used only when the content has none: a description written on the
    /// content's SEO field (<see cref="HeadMetadataDto.MetaDescription"/>) takes precedence.
    /// </summary>
    public string? FallbackDescription { get; set; }

    /// <summary>
    /// A share image for the page - a post's cover, say - as a file library address or an absolute URL. Used when
    /// the content's SEO field has none, in place of the site's default share image.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// <c>og:type</c>: <c>website</c>, the default, or <c>article</c>, which also writes <see cref="PublishedTime"/>
    /// and <see cref="ModifiedTime"/>.
    /// </summary>
    public string? OgType { get; set; }

    /// <summary>When the content was published, for <c>article:published_time</c>.</summary>
    public DateTime? PublishedTime { get; set; }

    /// <summary>When the content was last changed, for <c>article:modified_time</c>.</summary>
    public DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// Structured data: a schema.org node, or a list of them, as any value <see cref="JsonSerializer"/> writes as
    /// an object - a <c>Dictionary&lt;string, object?&gt;</c> lets a key be <c>"@type"</c>. Written by
    /// <see cref="SiteJsonLd"/>.
    /// </summary>
    public object? Schema { get; set; }

    /// <summary>
    /// The 1-based page of a paged list. Beyond page 1 every address written - canonical, og:url, each
    /// hreflang alternate and x-default - carries <see cref="PageQueryKey"/>: page 2 lists different items
    /// than page 1, so it must not canonicalise to it, and its alternates are the other languages' page 2.
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>The query parameter the host's pager writes the page number to.</summary>
    public string PageQueryKey { get; set; } = "page";

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        if (OgType != null && !SiteHeadOptions.OgTypes.Contains(OgType))
        {
            throw new ArgumentException(
                $"site-head-metadata's og-type is '{OgType}'; it can be {string.Join(" or ", SiteHeadOptions.OgTypes)}.");
        }

        var options = new SiteHeadOptions
        {
            Title = Title,
            FallbackDescription = FallbackDescription,
            Image = Image,
            OgType = OgType,
            PublishedTime = PublishedTime,
            ModifiedTime = ModifiedTime,
            JsonLd = Schema == null ? null : SiteJsonLd.Serialize(JsonSerializer.SerializeToNode(Schema), CreateUrlResolver())
        };

        foreach (var tag in SiteHeadMetadataHtml.BuildTags(Metadata, options, Paged))
        {
            output.Content.AppendHtml(tag).AppendHtml(Environment.NewLine);
        }
    }

    protected virtual string Paged(string url)
    {
        return SiteHeadMetadataHtml.Paged(url, CurrentPage, PageQueryKey);
    }

    /// <summary>
    /// How the structured data's addresses are made absolute: <c>~/...</c> in the request's language, as a
    /// <c>~/</c> link is, then on the site's primary domain (<see cref="SiteJsonLd.CreateUrlResolver"/>).
    /// </summary>
    protected virtual Func<string, string> CreateUrlResolver()
    {
        var services = ViewContext.HttpContext.RequestServices;
        var languageContext = services.GetRequiredService<SiteLanguageContext>();
        var languages = languageContext.GetLoadedLanguages();
        var cultureName = languageContext.GetCurrentCultureName(languages);
        var excludedPathPrefixes = services.GetRequiredService<IOptions<SiteLanguageOptions>>().Value.ExcludedPathPrefixes;

        return SiteJsonLd.CreateUrlResolver(
            Metadata?.BaseUrl,
            path => SiteLinks.Localize(path, cultureName, languages, excludedPathPrefixes));
    }
}
