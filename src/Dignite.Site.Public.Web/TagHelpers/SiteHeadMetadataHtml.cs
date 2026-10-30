using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using Dignite.Site.Public.Seo;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;

namespace Dignite.Site.Public.TagHelpers;

/// <summary>
/// The <c>&lt;head&gt;</c> tags of one resolved route - shared by <see cref="SiteHeadMetadataTagHelper"/> (Razor
/// templates) and the <c>site_head</c> function of Scriban templates, so the two engines cannot drift apart on
/// what a page's SEO head contains. What each tag is and when it is written is described on
/// <see cref="SiteHeadMetadataTagHelper"/>.
/// </summary>
public static class SiteHeadMetadataHtml
{
    /// <summary>
    /// The tags as HTML, each followed by <see cref="Environment.NewLine"/>.
    /// </summary>
    /// <param name="metadata">The route's head metadata. Null - no route matched - writes only <paramref name="fallbackDescription"/>.</param>
    /// <param name="title">The page's own title for <c>og:title</c>; falls back to <see cref="HeadMetadataDto.MetaTitle"/>.</param>
    /// <param name="fallbackDescription">A template's description, used only when the content has none.</param>
    /// <param name="currentPage">The 1-based page of a paged list; beyond 1, every address carries <paramref name="pageQueryKey"/>.</param>
    /// <param name="pageQueryKey">The query parameter the host's pager writes the page number to.</param>
    /// <param name="encoder">
    /// Encodes the attribute values - the host's own <see cref="HtmlEncoder"/>, as Razor would use, when the
    /// caller has it; <see cref="HtmlEncoder.Default"/> otherwise.
    /// </param>
    public static string Build(
        HeadMetadataDto? metadata,
        string? title,
        string? fallbackDescription,
        int currentPage,
        string pageQueryKey,
        HtmlEncoder? encoder = null)
    {
        var tags = BuildTags(metadata, title, fallbackDescription, url => Paged(url, currentPage, pageQueryKey));

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        foreach (var tag in tags)
        {
            tag.WriteTo(writer, encoder ?? HtmlEncoder.Default);
            writer.Write(Environment.NewLine);
        }

        return writer.ToString();
    }

    /// <summary>
    /// The tags themselves, with the paging of every address left to <paramref name="paged"/> - the form
    /// <see cref="SiteHeadMetadataTagHelper"/> uses, so its tags are still encoded by the view's own encoder and
    /// its overridable <c>Paged</c> keeps working.
    /// </summary>
    public static IReadOnlyList<TagBuilder> BuildTags(
        HeadMetadataDto? metadata,
        string? title,
        string? fallbackDescription,
        Func<string, string> paged)
    {
        var description = !string.IsNullOrWhiteSpace(metadata?.MetaDescription)
            ? metadata!.MetaDescription
            : fallbackDescription;

        var tags = new List<TagBuilder>();

        if (!string.IsNullOrWhiteSpace(description))
        {
            tags.Add(Meta("name", "description", description));
        }

        if (metadata != null)
        {
            var canonicalUrl = paged(metadata.CanonicalUrl);

            tags.Add(Link("canonical", canonicalUrl));

            if (!string.IsNullOrWhiteSpace(metadata.RobotsContent))
            {
                tags.Add(Meta("name", "robots", metadata.RobotsContent));
            }

            tags.Add(Meta("property", "og:title", string.IsNullOrWhiteSpace(title) ? metadata.MetaTitle : title));

            if (!string.IsNullOrWhiteSpace(description))
            {
                tags.Add(Meta("property", "og:description", description));
            }

            tags.Add(Meta("property", "og:url", canonicalUrl));

            if (!string.IsNullOrWhiteSpace(metadata.OgType))
            {
                tags.Add(Meta("property", "og:type", metadata.OgType));
            }

            if (!string.IsNullOrWhiteSpace(metadata.OgImageUrl))
            {
                tags.Add(Meta("property", "og:image", metadata.OgImageUrl));
            }

            if (!string.IsNullOrWhiteSpace(metadata.TwitterCardType))
            {
                tags.Add(Meta("name", "twitter:card", metadata.TwitterCardType));
            }

            foreach (var alternate in metadata.HreflangAlternates)
            {
                tags.Add(Alternate(alternate.CultureName, paged(alternate.Url)));
            }

            if (!string.IsNullOrWhiteSpace(metadata.XDefaultUrl))
            {
                tags.Add(Alternate("x-default", paged(metadata.XDefaultUrl)));
            }
        }

        return tags;
    }

    /// <summary><paramref name="url"/> with the page number, beyond page 1.</summary>
    public static string Paged(string url, int currentPage, string pageQueryKey)
    {
        return currentPage > 1
            ? QueryHelpers.AddQueryString(url, pageQueryKey, currentPage.ToString(CultureInfo.InvariantCulture))
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
