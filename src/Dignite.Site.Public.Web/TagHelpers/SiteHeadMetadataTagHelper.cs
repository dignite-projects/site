using System;
using System.Collections.Generic;
using System.Globalization;
using Dignite.Site.Public.Seo;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.WebUtilities;

namespace Dignite.Site.Public.TagHelpers;

/// <summary>
/// Writes one resolved route's <c>&lt;head&gt;</c> tags from its <see cref="HeadMetadataDto"/> - description,
/// canonical, robots, Open Graph, Twitter card, hreflang and x-default - so a host's layout does not each
/// spell them out by hand, e.g.
/// <c>&lt;site-head-metadata metadata="@Model.HeadMetadata" title="@ViewBag.Title" /&gt;</c> inside the
/// section the host's layout renders into <c>&lt;head&gt;</c>.
/// <para>
/// Only the tags that come from the route. <c>&lt;title&gt;</c>, charset and viewport belong to the host's
/// own layout, which knows its branding; JSON-LD is the host's own choice as well (总体设计 §5.4).
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
    /// The 1-based page of a paged list. Beyond page 1 every address written - canonical, og:url, each
    /// hreflang alternate and x-default - carries <see cref="PageQueryKey"/>: page 2 lists different items
    /// than page 1, so it must not canonicalise to it, and its alternates are the other languages' page 2.
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>The query parameter the host's pager writes the page number to.</summary>
    public string PageQueryKey { get; set; } = "page";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        var description = !string.IsNullOrWhiteSpace(Metadata?.MetaDescription)
            ? Metadata!.MetaDescription
            : FallbackDescription;

        var tags = new List<TagBuilder>();

        if (!string.IsNullOrWhiteSpace(description))
        {
            tags.Add(Meta("name", "description", description));
        }

        if (Metadata != null)
        {
            var canonicalUrl = Paged(Metadata.CanonicalUrl);

            tags.Add(Link("canonical", canonicalUrl));

            if (!string.IsNullOrWhiteSpace(Metadata.RobotsContent))
            {
                tags.Add(Meta("name", "robots", Metadata.RobotsContent));
            }

            tags.Add(Meta("property", "og:title", string.IsNullOrWhiteSpace(Title) ? Metadata.MetaTitle : Title));

            if (!string.IsNullOrWhiteSpace(description))
            {
                tags.Add(Meta("property", "og:description", description));
            }

            tags.Add(Meta("property", "og:url", canonicalUrl));

            if (!string.IsNullOrWhiteSpace(Metadata.OgType))
            {
                tags.Add(Meta("property", "og:type", Metadata.OgType));
            }

            if (!string.IsNullOrWhiteSpace(Metadata.OgImageUrl))
            {
                tags.Add(Meta("property", "og:image", Metadata.OgImageUrl));
            }

            if (!string.IsNullOrWhiteSpace(Metadata.TwitterCardType))
            {
                tags.Add(Meta("name", "twitter:card", Metadata.TwitterCardType));
            }

            foreach (var alternate in Metadata.HreflangAlternates)
            {
                tags.Add(Alternate(alternate.CultureName, Paged(alternate.Url)));
            }

            if (!string.IsNullOrWhiteSpace(Metadata.XDefaultUrl))
            {
                tags.Add(Alternate("x-default", Paged(Metadata.XDefaultUrl)));
            }
        }

        foreach (var tag in tags)
        {
            output.Content.AppendHtml(tag).AppendHtml(Environment.NewLine);
        }
    }

    protected virtual string Paged(string url)
    {
        return CurrentPage > 1
            ? QueryHelpers.AddQueryString(url, PageQueryKey, CurrentPage.ToString(CultureInfo.InvariantCulture))
            : url;
    }

    private static TagBuilder Meta(string keyAttribute, string key, string content)
    {
        var tag = new TagBuilder("meta") { TagRenderMode = TagRenderMode.StartTag };
        tag.Attributes[keyAttribute] = key;
        tag.Attributes["content"] = content;
        return tag;
    }

    private static TagBuilder Link(string rel, string href)
    {
        var tag = new TagBuilder("link") { TagRenderMode = TagRenderMode.StartTag };
        tag.Attributes["rel"] = rel;
        tag.Attributes["href"] = href;
        return tag;
    }

    private static TagBuilder Alternate(string hreflang, string href)
    {
        var tag = Link("alternate", href);
        tag.Attributes["hreflang"] = hreflang;
        return tag;
    }
}
