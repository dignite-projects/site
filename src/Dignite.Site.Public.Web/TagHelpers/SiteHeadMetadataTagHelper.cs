using System;
using Dignite.Site.Public.Seo;
using Microsoft.AspNetCore.Razor.TagHelpers;

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

        foreach (var tag in SiteHeadMetadataHtml.BuildTags(Metadata, Title, FallbackDescription, Paged))
        {
            output.Content.AppendHtml(tag).AppendHtml(Environment.NewLine);
        }
    }

    protected virtual string Paged(string url)
    {
        return SiteHeadMetadataHtml.Paged(url, CurrentPage, PageQueryKey);
    }
}
