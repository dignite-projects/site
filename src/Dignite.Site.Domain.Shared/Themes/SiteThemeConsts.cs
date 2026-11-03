using System;
using System.Collections.Generic;
using Dignite.Site.Pages;

namespace Dignite.Site.Themes;

/// <summary>
/// Limits and the few names the theme relies on. Mutable statics (like <see cref="PageConsts"/>) where a host
/// may widen a limit before its model is built.
/// <para>
/// No directory is reserved for templates. A file is a static file when its name has an extension (<c>site.css</c>)
/// and a Scriban template when it has none (<c>Events</c>, <c>_card</c>, <c>layouts/default</c>); what a template does
/// in a render - page, partial or layout - follows from what refers to it. See <see cref="SiteThemePaths"/>. The one
/// folder with a meaning of its own is <see cref="TextFilesFolder"/>, which holds the theme's texts
/// (<see cref="SiteThemeTextFiles"/>).
/// </para>
/// </summary>
public static class SiteThemeConsts
{
    /// <summary>
    /// Same as <see cref="PageConsts.MaxTemplateLength"/>, because a template is found by the value
    /// <c>Page.Template</c> holds. A property rather than a copied number, so the two cannot drift apart.
    /// </summary>
    public static int MaxPathLength => PageConsts.MaxTemplateLength;

    /// <summary>
    /// A path: segments of letters, digits, <c>_</c> and <c>-</c>, joined by <c>/</c>, the first character of each
    /// a letter, a digit or <c>_</c>; only the last segment may carry an extension (<c>.css</c>, <c>.min.js</c>).
    /// So <c>..</c>, an empty segment, a hidden-file name and a dotted directory cannot be spelled, and a path
    /// without an extension is also a value <c>Page.Template</c> accepts.
    /// </summary>
    public const string PathPattern =
        @"^(?:[A-Za-z0-9_][A-Za-z0-9_\-]*/)*[A-Za-z0-9_][A-Za-z0-9_\-]*(?:\.[A-Za-z0-9_\-]+)*$";

    /// <summary>Default value: 256 KiB of source per file.</summary>
    public static int MaxBodyLength { get; set; } = 256 * 1024;

    /// <summary>Default value: 256. The note that says what a release changed.</summary>
    public static int MaxCommentLength { get; set; } = 256;

    /// <summary>A SHA-256 as 64 lower-case hex digits.</summary>
    public const int ContentHashLength = 64;

    /// <summary>
    /// The extensions a static file may have, without the dot and in lower case. A file with any other extension is
    /// refused, so a template saved as <c>post.html</c> is an error at once rather than a file served as it is and
    /// never run. A host serving other kinds of text adds them here, and a <c>Content-Type</c> for them in the
    /// asset endpoint.
    /// </summary>
    public static ISet<string> AssetExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "css", "js", "mjs", "json", "map", "txt", "svg", "xml", "webmanifest", "ics", "csv"
    };

    /// <summary>
    /// The folder of the theme's texts, one <c>{culture}.json</c> file per language: <c>locales/ja.json</c>.
    /// See <see cref="SiteThemeTextFiles"/>.
    /// </summary>
    public const string TextFilesFolder = "locales";

    /// <summary>The default layout a new theme starts with; a setting of the theme, which the author may change or clear.</summary>
    public const string InitialDefaultLayout = "layouts/default";

    /// <summary>The front matter value that asks for no layout at all: <c>layout = "none"</c>.</summary>
    public const string NoLayout = "none";

    /// <summary>The one front matter key a page template may set.</summary>
    public const string LayoutKey = "layout";

    /// <summary>
    /// The first line that opens a front matter block, and the line that closes it. A template has front matter
    /// only when its very first line is exactly this.
    /// </summary>
    public const string FrontMatterMarker = "+++";

    /// <summary>The variable a layout reads to place the rendered page template.</summary>
    public const string BodyVariableName = "body";

    /// <summary>The function that emits the page's SEO head tags (title, meta, canonical, hreflang, ...).</summary>
    public const string SiteHeadFunctionName = "site_head";

    /// <summary>The function that renders another template in place.</summary>
    public const string IncludeFunctionName = "include";

    /// <summary>The function that writes the versioned address of a static file.</summary>
    public const string AssetUrlFunctionName = "asset_url";
}
