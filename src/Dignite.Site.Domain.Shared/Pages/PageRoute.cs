using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Volo.Abp.Text.Formatting;

namespace Dignite.Site.Pages;

/// <summary>
/// A page's route, in the form it is actually stored: a template, not just a base path (总体设计 §3.3).
/// A page with no placeholder - <c>/about</c>, <c>/</c> - has no content beneath it; one with
/// <c>{slug}</c> embedded - <c>/blog/{slug}</c> - requires every content beneath it to have its own slug;
/// one with <c>{slug?}</c> - <c>/about/{slug?}</c> - allows an empty slug too, for a default content at
/// the page's own address alongside others that have their own. Whether, and how, a page has content is
/// therefore never a stored flag, only ever read off the route itself.
/// <para>
/// A route may also carry any other named placeholder - <c>{publishTime:yyyy-MM}</c>,
/// <c>{category}</c> - referring to any field the content beneath it has, system property or
/// <c>FlexFields</c> business field alike (总体设计 §2.4). This class only ever parses the placeholder's
/// <i>name</i>; resolving what it is a name <i>of</i> is <see cref="Page.BuildContentPath"/>'s job, one
/// layer up, because that requires reading an actual <c>Content</c> - something this project deliberately
/// stays independent of. Unlike <c>{slug}</c>/<c>{slug?}</c>, every other placeholder is purely decorative
/// in the URL: <see cref="TryMatchSlug"/> only ever reads the slug back out, because <c>Slug</c> is the
/// only piece of a route that identifies a content (总体设计 §2.4's natural key).
/// </para>
/// <para>
/// Both directions live here, and they have to agree: <see cref="Build"/> composes a content's URL when
/// one is emitted (sitemap, canonical, hreflang), and <see cref="TryMatchSlug"/> takes a request path
/// apart again when it is routed. A route that builds one way and parses another produces URLs the site
/// itself 404s on. Parsing is delegated to ABP's own <see cref="FormattedStringValueExtracter"/> rather
/// than a hand-rolled regex translator - it locates each placeholder's value by the literal text that
/// follows it, which is exactly why any <c>:FORMAT</c> suffix is restricted to characters that can never
/// collide with a route's own literal separators (see <see cref="IsValid"/>): a formatted value that
/// happened to contain <c>/</c> would be indistinguishable from the next path segment, and the wrong
/// substring would be attributed to each placeholder.
/// </para>
/// <para>
/// This is routing, which is the page's job - it says nothing about whether the page renders as a single
/// page, a list or a detail view, so it does not reintroduce the "Kind" the model deliberately omits.
/// Unlike Dignite.Cms's <c>Section</c>, nothing here requires a route to contain <c>{slug}</c> - Cms can
/// demand that because <c>SectionType</c> already says whether a section carries entries; Site has no
/// such flag, so the placeholder's presence is the only signal there is.
/// </para>
/// <para>
/// Every method here assumes its <c>route</c>/<c>path</c> argument is already normalized by
/// <see cref="Page.NormalizeRoute"/> - leading slash, no trailing one. None of them normalize it again.
/// </para>
/// </summary>
public static class PageRoute
{
    private const string SlugToken = "{slug}";

    private const string OptionalSlugToken = "{slug?}";

    /// <summary>{slug?} reduced to plain {slug} - the one form every other method here actually parses against.</summary>
    private static string Canonicalize(string route)
    {
        return route.Replace(OptionalSlugToken, SlugToken, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether <paramref name="value"/> - a placeholder's captured text - spans more than one path segment.
    /// Shared by <see cref="TryMatchSlug"/> and <see cref="TryMatchPartial"/>, whose last-placeholder
    /// captures are otherwise unbounded (see each method's own remarks for why).
    /// </summary>
    private static bool ContainsPathSeparator(string value) => value.Contains('/');

    /// <summary>
    /// Matches any named placeholder - <c>{name}</c> or <c>{name:FORMAT}</c> - capturing the name in
    /// group 1 and the format, when present, in group 3. This also matches <c>{slug}</c> itself (a name
    /// is a name), which is harmless: <see cref="Build"/> and <see cref="TryMatchSlug"/> resolve it like
    /// any other placeholder once <c>{slug?}</c> has been canonicalized down to it. It does <i>not</i>
    /// match <c>{slug?}</c> - the trailing <c>?</c> is not a valid name character - which is exactly what
    /// keeps that token special enough for <see cref="IsSlugOptional"/> to recognize on its own.
    /// </summary>
    private static readonly Regex PlaceholderPattern =
        new(@"\{([a-zA-Z][a-zA-Z0-9]*)(:([^}]+))?\}", RegexOptions.Compiled);

    /// <summary>
    /// The characters a placeholder's <c>:FORMAT</c> suffix may use - letters, digits, <c>.</c>, <c>_</c>,
    /// <c>-</c>. Not a rule about dates specifically: it applies to every placeholder's format, whatever
    /// field it names, because the reason for it has nothing to do with what the field means. It is
    /// purely about what <see cref="FormattedStringValueExtracter"/> can safely parse back out - see the
    /// class remarks. <c>/</c> is the one character that must never appear in a formatted value, since
    /// that is what a route uses to separate placeholders from each other.
    /// </summary>
    private static readonly Regex FormatCharacters = new(@"^[a-zA-Z0-9_.-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Whether <paramref name="route"/> is usable.
    /// <para>
    /// <c>{slug}</c> and <c>{slug?}</c> are mutually exclusive - a route asking for both at once has no
    /// coherent meaning. Every other <c>{...}</c> token must be a well-formed <c>{name}</c> or
    /// <c>{name:FORMAT}</c> placeholder, with <c>FORMAT</c> restricted to <see cref="FormatCharacters"/> -
    /// whether <c>name</c> actually refers to a field the content has is not checked here, and cannot be:
    /// that requires reading a <c>ContentType</c>'s declared fields, which a route, on its own, has no way
    /// to reach. A route with no <c>{slug}</c>/<c>{slug?}</c> at all is valid - that is a page with
    /// nothing beneath it, not an error. See the class remarks for why this does not mirror
    /// Dignite.Cms's stricter rule.
    /// </para>
    /// <para>
    /// When present, <c>{slug}</c>/<c>{slug?}</c> must be the route's <i>last</i> placeholder - what makes
    /// "the placeholders before slug" in <see cref="TryMatchPartial"/> a well-defined, contiguous prefix
    /// to cut at all, rather than a set with no fixed order.
    /// </para>
    /// </summary>
    public static bool IsValid(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return false;
        }

        if (route.Contains(SlugToken, StringComparison.OrdinalIgnoreCase) &&
            route.Contains(OptionalSlugToken, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match placeholder in PlaceholderPattern.Matches(route))
        {
            var format = placeholder.Groups[3].Value;
            if (format.Length > 0 && !FormatCharacters.IsMatch(format))
            {
                return false;
            }

            // FormattedStringValueExtracter keys a captured value by the placeholder's whole inner text -
            // "publishTime:yyyy" and "publishTime:MM" are two distinct keys, which is exactly what lets a
            // route split one field across two differently-formatted segments (e.g. a /2026/07/ path).
            // What must never repeat is this same combined key - name and format both alike - since
            // TryMatchPartial folds every capture into one OrdinalIgnoreCase dictionary keyed by it, and a
            // second entry with an identical key would throw there instead of silently overwriting.
            var key = format.Length > 0 ? $"{placeholder.Groups[1].Value}:{format}" : placeholder.Groups[1].Value;
            if (!seenKeys.Add(key))
            {
                return false;
            }
        }

        if (HasSlug(route))
        {
            var canonical = Canonicalize(route);
            var placeholders = PlaceholderPattern.Matches(canonical);
            var last = placeholders[^1];

            if (!string.Equals(last.Groups[1].Value, "slug", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Anything left with a brace in it matched neither a recognized placeholder above nor {slug?} -
        // a typo, or a token from a different vocabulary someone remembered. Catching it here, at the one
        // place a route is accepted, is what stops that from being a silent trap discovered only after
        // content exists at the bad URL.
        var withoutRecognizedPlaceholders = PlaceholderPattern
            .Replace(route, string.Empty)
            .Replace(OptionalSlugToken, string.Empty, StringComparison.Ordinal);

        return !withoutRecognizedPlaceholders.Contains('{') && !withoutRecognizedPlaceholders.Contains('}');
    }

    /// <summary>
    /// The address the page itself sits at, independent of anything beneath it: everything up to (but not
    /// including) the path segment that first carries a placeholder. A route with no placeholder is
    /// already its own address.
    /// <para>
    /// This is what sitemap, canonical, hreflang and feed URLs are built from (<see cref="Page.GetPath"/>)
    /// - never the stored route directly, since that may be a template. Building from the raw route would
    /// emit the literal placeholder text into every one of those URLs.
    /// </para>
    /// </summary>
    public static string GetPath(string route)
    {
        var placeholderIndex = route.IndexOf('{');
        if (placeholderIndex < 0)
        {
            return route;
        }

        var segmentBoundary = route.LastIndexOf('/', placeholderIndex);
        return segmentBoundary <= 0 ? "/" : route[..segmentBoundary];
    }

    /// <summary>
    /// Whether <paramref name="route"/> carries any placeholder at all - a template rather than a plain
    /// literal path. This is the tie-break a literal route wins when it shares its own address with a
    /// template (总体设计 §3.4): a route this returns false for is already its own address, never a
    /// derived one, so it is always the more specific match.
    /// </summary>
    public static bool IsTemplate(string route) => route.IndexOf('{') >= 0;

    /// <summary>Whether <paramref name="route"/> carries a <c>{slug}</c> or <c>{slug?}</c> - whether the page has content beneath it.</summary>
    public static bool HasSlug(string route)
    {
        return route.Contains(SlugToken, StringComparison.OrdinalIgnoreCase) ||
               route.Contains(OptionalSlugToken, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether an empty slug is allowed beneath <paramref name="route"/> - whether it uses <c>{slug?}</c>
    /// rather than the plain, mandatory <c>{slug}</c>. Meaningless (and always false) when
    /// <see cref="HasSlug"/> is false - there is no slug to be optional about.
    /// </summary>
    public static bool IsSlugOptional(string route)
    {
        return route.Contains(OptionalSlugToken, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Renders the absolute path of one content under this route, e.g. <c>/news/2026-07/my-post</c>.
    /// </summary>
    /// <param name="route">The owning page's route.</param>
    /// <param name="valueResolver">
    /// Resolves one placeholder's rendered text from its name and optional format - <c>{slug}</c>
    /// included, like any other name. The caller owns what a name resolves <i>to</i>
    /// (<see cref="Page.BuildContentPath"/> reads it off a <c>Content</c>); this method only owns where
    /// each placeholder sits in the route.
    /// </param>
    public static string Build(string route, Func<string, string?, string> valueResolver)
    {
        var canonical = Canonicalize(route);

        return PlaceholderPattern.Replace(canonical, match =>
        {
            var name = match.Groups[1].Value;
            var format = match.Groups[3].Success ? match.Groups[3].Value : null;
            return valueResolver(name, format);
        });
    }

    /// <summary>
    /// The reverse of <see cref="Build"/>: reads the slug back out of a full request path, matched
    /// against the whole route as one anchored template - not a prefix plus a remainder.
    /// <para>
    /// Only the slug is returned, because only the slug identifies the content -
    /// <c>(PageId, CultureName, Slug)</c> is the unique constraint, so any other placeholder is decoration
    /// that has already done its job by matching, whatever text it actually captured. Requiring a date
    /// segment to agree with the content's stored publish time would mean an editor who reschedules a
    /// post breaks every link to it - the same reasoning applies to any other field a route happens to
    /// display, so nothing here checks that a decorative placeholder's captured text looks like a real
    /// value of its type.
    /// </para>
    /// <para>
    /// The one thing that <i>is</i> checked: when <c>{slug}</c> is the route's last placeholder,
    /// <see cref="FormattedStringValueExtracter"/> gives it everything left in the path, slashes included
    /// - it has no notion that a slug is exactly one segment. A path with more segments than the route
    /// expects would otherwise still "match", with the extra segments folded into the slug. Rejecting a
    /// captured slug that contains <c>/</c> is what keeps <c>/blog/{slug}</c> from matching
    /// <c>/blog/2026/07/my-post</c>.
    /// </para>
    /// <para>
    /// Matched case-sensitively - a route's literal text (<c>"post-"</c> in <c>/blog/post-{slug}</c>, say)
    /// is not a placeholder name and gets no case-insensitive treatment of its own. <see cref="Build"/>
    /// only ever emits one casing, so matching a differently-cased request the same as the canonical one
    /// would accept unlimited case-variant duplicate URLs for the same content with no way to tell them
    /// apart or redirect to the canonical form - the same class of drift <see cref="GetPath"/>'s own
    /// case-sensitive address matching already refuses to allow.
    /// </para>
    /// </summary>
    /// <param name="route">The owning page's route.</param>
    /// <param name="path">The full, normalized request path.</param>
    public static bool TryMatchSlug(string route, string path, out string slug)
    {
        slug = string.Empty;

        var canonical = Canonicalize(route);
        var result = FormattedStringValueExtracter.Extract(path, canonical, ignoreCase: false);

        if (!result.IsMatch)
        {
            return false;
        }

        foreach (var match in result.Matches)
        {
            if (string.Equals(match.Name, "slug", StringComparison.OrdinalIgnoreCase))
            {
                slug = match.Value;
                break;
            }
        }

        if (ContainsPathSeparator(slug))
        {
            slug = string.Empty;
            return false;
        }

        return slug.Length > 0;
    }

    /// <summary>
    /// A looser cousin of <see cref="TryMatchSlug"/>: matches <paramref name="path"/> against the largest
    /// prefix of <paramref name="route"/>'s placeholders - short of <c>{slug}</c>/<c>{slug?}</c> itself -
    /// that <paramref name="path"/> actually satisfies. Where <see cref="TryMatchSlug"/> requires every
    /// placeholder filled, this requires at least one but never all of them: a route with placeholders
    /// before its slug also answers a path naming only some leading subset of those placeholders -
    /// <c>/blog/abc/{publishTime:yyyy-MM}/{slug}</c> answers <c>/blog/abc/2026-08</c> with
    /// <c>publishTime:yyyy-MM = 2026-08</c>, one placeholder short of a slug. <see cref="IsValid"/>
    /// requiring slug, when present, to be the route's last placeholder is what makes "the placeholders
    /// before slug" a well-defined, contiguous prefix to cut at all.
    /// <para>
    /// Tried from the deepest cut (every placeholder but slug filled) down to the shallowest (just the
    /// first): each cut is itself a complete <see cref="FormattedStringValueExtracter"/> template, matched
    /// the same all-or-nothing way <see cref="TryMatchSlug"/> matches a full route, just against a shorter
    /// prefix of it - not the extracter doing partial matching, which it cannot (its own contract is
    /// "fully matched" or nothing).
    /// </para>
    /// <para>
    /// A shallower cut's last placeholder is exactly as unbounded as <see cref="TryMatchSlug"/>'s slug is
    /// when nothing follows it in the template - rejecting a captured value that contains <c>/</c> is what
    /// keeps a shallow cut from swallowing path segments that actually belong to a deeper one, the same
    /// reasoning <see cref="TryMatchSlug"/> already applies to slug itself.
    /// </para>
    /// <para>
    /// Callers decide which page's route to even try this against - by the time this runs, the caller
    /// (<c>SiteRouteResolver</c>) has already settled which page a request belongs to; this only answers
    /// what the rest of the path means to that page.
    /// </para>
    /// <para>
    /// Matched case-sensitively, for the same reason <see cref="TryMatchSlug"/> is - see its own remarks.
    /// </para>
    /// </summary>
    /// <param name="route">The owning page's route.</param>
    /// <param name="path">The full, normalized request path.</param>
    public static bool TryMatchPartial(string route, string path, out IReadOnlyDictionary<string, string> values)
    {
        values = EmptyValues;

        var canonical = Canonicalize(route);
        var placeholders = PlaceholderPattern.Matches(canonical);

        // cut counts how many placeholders before slug are filled - from all of them down to just the
        // first. cut == 0 (nothing filled, i.e. the route's own bare address) is GetPath's territory, not
        // this method's.
        for (var cut = placeholders.Count - 1; cut >= 1; cut--)
        {
            // Up to, but not including, the next (unfilled) placeholder - not just through the end of the
            // cut placeholder itself. Stopping at the cut placeholder's own end would silently drop any
            // literal text between it and the next placeholder (e.g. the "-archive" in
            // "{category}-archive/{publishTime}"), letting a path that skips that literal text match
            // anyway. TrimEnd('/') only removes the separator immediately before the dropped placeholder,
            // not any literal text that precedes it.
            var prefixTemplate = canonical[..placeholders[cut].Index].TrimEnd('/');

            var result = FormattedStringValueExtracter.Extract(path, prefixTemplate, ignoreCase: false);

            if (!result.IsMatch || result.Matches.Any(match => ContainsPathSeparator(match.Value)))
            {
                continue;
            }

            values = result.Matches.ToDictionary(
                match => match.Name, match => match.Value, StringComparer.OrdinalIgnoreCase);
            return true;
        }

        return false;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyValues = new Dictionary<string, string>();

    /// <summary>
    /// A few placeholders every route can use without anything else being configured first - surfaced so
    /// an admin UI or an MCP tool description can show examples instead of hard-coding its own copy. Not
    /// exhaustive: <see cref="IsValid"/> accepts <c>{name}</c>/<c>{name:FORMAT}</c> for any field name a
    /// content has, system property or <c>FlexFields</c> business field alike.
    /// </summary>
    public static IReadOnlyList<string> SupportedPlaceholders { get; } = new[]
    {
        SlugToken,
        OptionalSlugToken,
        "{publishTime:yyyy-MM}"
    };
}
