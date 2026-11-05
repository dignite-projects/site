using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Xml;
using Dignite.Site.Files;
using Dignite.Site.Public.Seo;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Html;
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
        return Build(
            metadata,
            new SiteHeadOptions { Title = title, FallbackDescription = fallbackDescription },
            currentPage,
            pageQueryKey,
            encoder);
    }

    /// <summary>
    /// The tags as HTML, each followed by <see cref="Environment.NewLine"/>, with what the template adds in
    /// <paramref name="options"/>.
    /// </summary>
    public static string Build(
        HeadMetadataDto? metadata,
        SiteHeadOptions options,
        int currentPage,
        string pageQueryKey,
        HtmlEncoder? encoder = null)
    {
        var tags = BuildTags(metadata, options, url => Paged(url, currentPage, pageQueryKey));

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        foreach (IHtmlContent tag in tags)
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
        return BuildTags(metadata, new SiteHeadOptions { Title = title, FallbackDescription = fallbackDescription }, paged);
    }

    /// <summary>
    /// The tags themselves, with what the template adds in <paramref name="options"/> and the paging of every
    /// address left to <paramref name="paged"/>.
    /// </summary>
    public static IReadOnlyList<TagBuilder> BuildTags(
        HeadMetadataDto? metadata,
        SiteHeadOptions options,
        Func<string, string> paged)
    {
        var description = !string.IsNullOrWhiteSpace(metadata?.MetaDescription)
            ? metadata!.MetaDescription
            : options.FallbackDescription;

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

            tags.Add(Meta("property", "og:title", string.IsNullOrWhiteSpace(options.Title) ? metadata.MetaTitle : options.Title));

            if (!string.IsNullOrWhiteSpace(description))
            {
                tags.Add(Meta("property", "og:description", description));
            }

            tags.Add(Meta("property", "og:url", canonicalUrl));

            var ogType = string.IsNullOrWhiteSpace(options.OgType) ? metadata.OgType : options.OgType;
            if (!string.IsNullOrWhiteSpace(ogType))
            {
                tags.Add(Meta("property", "og:type", ogType));
            }

            if (!string.IsNullOrWhiteSpace(metadata.SiteName))
            {
                tags.Add(Meta("property", "og:site_name", metadata.SiteName));
            }

            if (!string.IsNullOrWhiteSpace(metadata.Locale))
            {
                tags.Add(Meta("property", "og:locale", metadata.Locale));
            }

            // Null-safe: a DTO that came over HTTP can carry a null list.
            foreach (var locale in metadata.LocaleAlternates ?? new List<string>())
            {
                tags.Add(Meta("property", "og:locale:alternate", locale));
            }

            var image = ResolveShareImage(metadata, options.Image);
            if (image != null)
            {
                tags.Add(Meta("property", "og:image", image.Url));
                if (image.Width != null && image.Height != null)
                {
                    tags.Add(Meta("property", "og:image:width", image.Width.Value.ToString(CultureInfo.InvariantCulture)));
                    tags.Add(Meta("property", "og:image:height", image.Height.Value.ToString(CultureInfo.InvariantCulture)));
                }
            }

            if (ogType == SiteHeadOptions.ArticleType)
            {
                if (options.PublishedTime != null)
                {
                    tags.Add(Meta("property", "article:published_time", FormatTime(options.PublishedTime.Value)));
                }

                if (options.ModifiedTime != null)
                {
                    tags.Add(Meta("property", "article:modified_time", FormatTime(options.ModifiedTime.Value)));
                }
            }

            // The card follows the image the page ends up with, as Site decides it for the route's own image: a
            // template's image makes a large card of a page that would otherwise have none.
            var twitterCard = image != null ? "summary_large_image" : metadata.TwitterCardType;
            if (!string.IsNullOrWhiteSpace(twitterCard))
            {
                tags.Add(Meta("name", "twitter:card", twitterCard));
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

        if (!string.IsNullOrEmpty(options.JsonLd))
        {
            tags.Add(new JsonLdScriptTag(options.JsonLd));
        }

        return tags;
    }

    /// <summary>
    /// The share image a page ends up with: the one the content's author chose (its SEO field); else the template's
    /// <paramref name="templateImage"/>, sized and made absolute the way Site does its own; else the site's default
    /// share image; else none.
    /// </summary>
    public static SiteShareImage? ResolveShareImage(HeadMetadataDto metadata, string? templateImage)
    {
        var hasAuthorImage = !string.IsNullOrWhiteSpace(metadata.OgImageUrl) && !metadata.IsDefaultOgImage;
        if (!hasAuthorImage && !string.IsNullOrWhiteSpace(templateImage))
        {
            var url = SiteFileUrl.Absolutize(
                SiteFileUrl.Sized(templateImage.Trim(), OpenGraphConsts.ImageWidth, OpenGraphConsts.ImageHeight),
                metadata.BaseUrl ?? string.Empty)!;
            return SiteFileUrl.IsSiteFile(url)
                ? new SiteShareImage(url, OpenGraphConsts.ImageWidth, OpenGraphConsts.ImageHeight)
                : new SiteShareImage(url, null, null);
        }

        return string.IsNullOrWhiteSpace(metadata.OgImageUrl)
            ? null
            : new SiteShareImage(metadata.OgImageUrl, metadata.OgImageWidth, metadata.OgImageHeight);
    }

    /// <summary>A time as Open Graph's <c>article:*</c> properties take it: ISO 8601, with its offset when it has one.</summary>
    private static string FormatTime(DateTime time)
    {
        return XmlConvert.ToString(time, XmlDateTimeSerializationMode.RoundtripKind);
    }

    /// <summary><paramref name="url"/> with the page number, beyond page 1.</summary>
    public static string Paged(string url, int currentPage, string pageQueryKey)
    {
        return currentPage > 1
            ? QueryHelpers.AddQueryString(url, pageQueryKey, currentPage.ToString(CultureInfo.InvariantCulture))
            : url;
    }

    /// <summary>
    /// <c>&lt;script type="application/ld+json"&gt;</c> with the structured data, written as it is: SiteJsonLd has escaped
    /// <c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c> already, so nothing in it can end the script. Written by hand rather than
    /// by <see cref="TagBuilder"/>, whose encoder would write the type's <c>+</c> as <c>&amp;#x2B;</c>. It stays a
    /// <see cref="TagBuilder"/>, with the same attribute and content, for a caller that reads the list.
    /// </summary>
    private sealed class JsonLdScriptTag : TagBuilder, IHtmlContent
    {
        private const string Type = "application/ld+json";

        private readonly string _json;

        public JsonLdScriptTag(string json)
            : base("script")
        {
            _json = json;
            Attributes["type"] = Type;
            InnerHtml.AppendHtml(json);
        }

        void IHtmlContent.WriteTo(TextWriter writer, HtmlEncoder encoder)
        {
            writer.Write("<script type=\"" + Type + "\">");
            writer.Write(_json);
            writer.Write("</script>");
        }
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
