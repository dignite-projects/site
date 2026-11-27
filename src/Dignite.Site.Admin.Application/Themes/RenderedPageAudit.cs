using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Dignite.Site.Themes;

namespace Dignite.Site.Admin.Themes;

/// <summary>One thing a rendered page lacks: a <see cref="SiteThemeIssueCodes"/> value, and the element it is about, if any.</summary>
public sealed record RenderedPageFinding(string Code, string? Element = null);

/// <summary>
/// What a rendered page lacks of what search engines, AI assistants and link previews read from it. A theme can leave
/// any of it out and still render, so nothing fails; the trial render reads what it rendered for it and warns.
/// <para>
/// The checks are those of the established audits, each as it is defined there: Lighthouse's SEO audits for the title
/// (<c>document-title</c>), the description (<c>meta-description</c>) and the viewport (<c>viewport</c>); axe-core's rules,
/// which Lighthouse runs too, for the document's language (<c>html-has-lang</c>), images (<c>image-alt</c>), links
/// (<c>link-name</c>) and the main heading (<c>page-has-heading-one</c>); Google's advice to describe the organization behind
/// a site in structured data on its home page; and <c>og:image</c>, one of the four properties the Open Graph protocol
/// requires of every page.
/// </para>
/// <para>
/// Only whether a thing is there. Whether a title or an <c>alt</c> is good is for a person to judge, and a link's text is
/// not compared with words like "click here", as Lighthouse's <c>link-text</c> does: such a list is one per language, and
/// a site can be in any.
/// </para>
/// </summary>
public static class RenderedPageAudit
{
    /// <summary>The longest an element's address is quoted in a finding; a <c>data:</c> URL can be a whole image.</summary>
    public const int MaxElementLength = 120;

    /// <summary>The codes in the order they are reported.</summary>
    public static IReadOnlyList<string> Codes { get; } = new[]
    {
        SiteThemeIssueCodes.TitleMissing,
        SiteThemeIssueCodes.DescriptionMissing,
        SiteThemeIssueCodes.LanguageMissing,
        SiteThemeIssueCodes.ViewportMissing,
        SiteThemeIssueCodes.HeadingMissing,
        SiteThemeIssueCodes.ImageAltMissing,
        SiteThemeIssueCodes.LinkTextMissing,
        SiteThemeIssueCodes.ShareImageMissing,
        SiteThemeIssueCodes.StructuredDataMissing
    };

    private static readonly HtmlParser Parser = new();

    /// <summary>
    /// What <paramref name="html"/> lacks, once per code in the order of <see cref="Codes"/>, with the first image or link
    /// that lacks its text. <paramref name="isHomePage"/> adds the structured data the home page should carry. Nothing for
    /// an empty page: there is nothing to read.
    /// </summary>
    public static IReadOnlyList<RenderedPageFinding> Find(string? html, bool isHomePage)
    {
        var findings = new List<RenderedPageFinding>();
        if (string.IsNullOrWhiteSpace(html))
        {
            return findings;
        }

        using var document = Parser.ParseDocument(html);

        if (string.IsNullOrWhiteSpace(document.Title))
        {
            findings.Add(new RenderedPageFinding(SiteThemeIssueCodes.TitleMissing));
        }

        if (FindMeta(document, "name", "description") == null)
        {
            findings.Add(new RenderedPageFinding(SiteThemeIssueCodes.DescriptionMissing));
        }

        var root = document.DocumentElement;
        if (string.IsNullOrWhiteSpace(root.GetAttribute("lang")) && string.IsNullOrWhiteSpace(root.GetAttribute("xml:lang")))
        {
            findings.Add(new RenderedPageFinding(SiteThemeIssueCodes.LanguageMissing));
        }

        if (FindMeta(document, "name", "viewport") == null)
        {
            findings.Add(new RenderedPageFinding(SiteThemeIssueCodes.ViewportMissing));
        }

        if (!document.QuerySelectorAll("h1, [role=heading][aria-level='1']").Any(heading => !IsHidden(heading)))
        {
            findings.Add(new RenderedPageFinding(SiteThemeIssueCodes.HeadingMissing));
        }

        var image = document.QuerySelectorAll("img").FirstOrDefault(LacksAlternativeText);
        if (image != null)
        {
            findings.Add(new RenderedPageFinding(SiteThemeIssueCodes.ImageAltMissing, Quote(image.GetAttribute("src"))));
        }

        var link = document.QuerySelectorAll("a[href]").FirstOrDefault(element => LacksName(document, element));
        if (link != null)
        {
            findings.Add(new RenderedPageFinding(SiteThemeIssueCodes.LinkTextMissing, Quote(link.GetAttribute("href"))));
        }

        if (FindMeta(document, "property", "og:image") == null && FindMeta(document, "name", "og:image") == null)
        {
            findings.Add(new RenderedPageFinding(SiteThemeIssueCodes.ShareImageMissing));
        }

        if (isHomePage && !document.QuerySelectorAll("script[type]").Any(IsStructuredData))
        {
            findings.Add(new RenderedPageFinding(SiteThemeIssueCodes.StructuredDataMissing));
        }

        return findings;
    }

    /// <summary>The first <c>&lt;meta&gt;</c> whose <paramref name="attribute"/> is <paramref name="value"/>, ignoring case, with a content that is not blank.</summary>
    private static IElement? FindMeta(IDocument document, string attribute, string value)
    {
        return document.QuerySelectorAll("meta").FirstOrDefault(meta =>
            string.Equals(meta.GetAttribute(attribute)?.Trim(), value, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(meta.GetAttribute("content")));
    }

    /// <summary>
    /// axe-core's <c>image-alt</c>: an image needs an <c>alt</c>, which may be empty for one that only decorates, or a name
    /// from ARIA or <c>title</c>, unless it is marked as presentation or hidden from assistive technology.
    /// </summary>
    private static bool LacksAlternativeText(IElement image)
    {
        if (image.HasAttribute("alt") || IsHidden(image))
        {
            return false;
        }

        var role = image.GetAttribute("role")?.Trim();
        if (string.Equals(role, "presentation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "none", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(image.GetAttribute("aria-label")) &&
               string.IsNullOrWhiteSpace(image.GetAttribute("aria-labelledby")) &&
               string.IsNullOrWhiteSpace(image.GetAttribute("title"));
    }

    /// <summary>
    /// axe-core's <c>link-name</c>: a link needs a name a reader can be told - its text, the <c>alt</c> of an image in it, an
    /// <c>aria-label</c>, the text of what <c>aria-labelledby</c> names, or its <c>title</c>. A link hidden from assistive
    /// technology needs none.
    /// </summary>
    private static bool LacksName(IDocument document, IElement link)
    {
        if (IsHidden(link) ||
            !string.IsNullOrWhiteSpace(link.GetAttribute("aria-label")) ||
            !string.IsNullOrWhiteSpace(link.GetAttribute("title")) ||
            HasText(link))
        {
            return false;
        }

        var labelledBy = link.GetAttribute("aria-labelledby");
        return string.IsNullOrWhiteSpace(labelledBy) ||
               !labelledBy
                   .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                   .Select(document.GetElementById)
                   .Any(label => label != null && (!string.IsNullOrWhiteSpace(label.GetAttribute("aria-label")) || HasText(label)));
    }

    /// <summary>
    /// Whether <paramref name="element"/> holds text a reader is told: a text that is not blank, an image's <c>alt</c>, or an
    /// <c>aria-label</c>, in any element inside it that is not hidden. An SVG's <c>&lt;title&gt;</c> is text in it.
    /// </summary>
    private static bool HasText(IElement element)
    {
        foreach (var child in element.ChildNodes)
        {
            switch (child)
            {
                case IText text when !string.IsNullOrWhiteSpace(text.Data):
                    return true;
                case IElement inner when IsHiddenItself(inner) || inner.LocalName is "script" or "style" or "template":
                    continue;
                case IElement inner when !string.IsNullOrWhiteSpace(inner.GetAttribute("aria-label")):
                    return true;
                case IElement inner when inner.LocalName == "img" && !string.IsNullOrWhiteSpace(inner.GetAttribute("alt")):
                    return true;
                case IElement inner when HasText(inner):
                    return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="element"/>, or an element it is in, is hidden from readers.</summary>
    private static bool IsHidden(IElement element)
    {
        return element.Closest("[hidden], [aria-hidden='true']") != null;
    }

    /// <summary>Whether <paramref name="element"/> itself is hidden from readers; for a walk down a tree whose top is not.</summary>
    private static bool IsHiddenItself(IElement element)
    {
        return element.HasAttribute("hidden") ||
               string.Equals(element.GetAttribute("aria-hidden")?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStructuredData(IElement script)
    {
        return string.Equals(script.GetAttribute("type")?.Trim(), "application/ld+json", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(script.TextContent);
    }

    private static string Quote(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return string.Empty;
        }

        address = address.Trim();
        return address.Length <= MaxElementLength ? address : address[..MaxElementLength] + "…";
    }
}
