using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dignite.Site.Pages;

/// <summary>
/// A page's route, in the form it is actually stored: a template, not just a base path (总体设计 §3.3).
/// A page with no placeholder - <c>/about</c>, <c>/</c> - has no content beneath it; one with
/// <c>{slug}</c> embedded - <c>/blog/{slug}</c> - requires every content beneath it to have its own slug;
/// one with <c>{slug?}</c> - <c>/about/{slug?}</c> - allows an empty slug too, for a default content at
/// the page's own address alongside others that have their own. Whether, and how, a page has content is
/// therefore never a stored flag, only ever read off the route itself.
/// <para>
/// A route may also carry any other named placeholder, in one of four <c>:</c>-separated shapes -
/// <c>{name}</c>, <c>{name:FORMAT}</c>, <c>{name:REGEX}</c>, <c>{name:FORMAT:REGEX}</c> - referring to
/// any field the content beneath it has, system property or <c>FlexFields</c> business field alike
/// (总体设计 §2.4). This class only ever parses the placeholder's <i>name</i> (plus, now, its optional
/// format and regex); resolving what the name is a name <i>of</i> is <see cref="Page.BuildContentPath"/>'s
/// job, one layer up, because that requires reading an actual <c>Content</c> - something this project
/// deliberately stays independent of. Unlike <c>{slug}</c>/<c>{slug?}</c>, every other placeholder is
/// purely decorative in the URL: <see cref="TryMatchSlug"/> only ever reads the slug back out, because
/// <c>Slug</c> is the only piece of a route that identifies a content (总体设计 §2.4's natural key).
/// </para>
/// <para>
/// A single <c>:</c> after a name is ambiguous on its own - <c>{publishTime:yyyy-MM}</c> and
/// <c>{time:^\d{2}:\d{2}$}</c> are both exactly one segment. It is resolved by content, not position: a
/// segment made up only of <see cref="FormatCharacters"/> is a format (this is what keeps every existing
/// <c>{name:FORMAT}</c> route classified exactly as it always was - a format string, by construction,
/// can never also look like a regex worth compiling); anything else is tried as a regex, and a route
/// whose segment is neither a valid format nor a compilable regex is rejected outright. Two colons
/// resolve unambiguously the same way - the first segment must be a format, the rest (free to contain
/// further colons, e.g. a time regex) is the regex.
/// </para>
/// <para>
/// Both directions live here, and they have to agree: <see cref="Build"/> composes a content's URL when
/// one is emitted (sitemap, canonical, hreflang) - stripping every placeholder down to its name and
/// format, regex included or not, since a regex constrains what may be read back out of a request, not
/// what gets written into one - and <see cref="TryMatchSlug"/> takes a request path apart again when it
/// is routed, judging a regex-bearing placeholder's captured text against that regex (never its format,
/// which was never validated on the way in either). A route that builds one way and parses another
/// produces URLs the site itself 404s on.
/// </para>
/// <para>
/// Parsing used to be delegated to ABP's own <c>FormattedStringValueExtracter</c>, matching each
/// placeholder's value by the literal text that follows it. That stops working the moment a placeholder's
/// regex itself contains a literal <c>{</c>/<c>}</c> - a bracketed quantifier like <c>\d{4}</c> is
/// extremely ordinary regex syntax, and ABP's own tokenizer throws the instant it sees a second
/// <c>{</c> while still inside one it opened. This class now owns both the brace-depth-aware scan that
/// locates each placeholder's true closing brace (a bare backslash escapes the next character, so
/// <c>\{</c>/<c>\}</c> inside a regex do not perturb the count; an unescaped, unpaired literal <c>}</c>
/// inside a regex character class such as <c>[{}]</c> remains an unsupported, undocumented-workaround
/// edge case) and the boundary-matching extraction that used to be ABP's - faithfully reproducing its
/// algorithm (literal text located by <c>IndexOf</c>/<c>StartsWith</c>, whatever sits between two
/// boundaries captured for the placeholder before it), with one bug fixed along the way: the original
/// never verified a trailing literal token actually consumed the rest of the input, so
/// <c>Extract("/shop/furniture/2026-08", "/shop/furniture")</c> would report a match with zero captures
/// instead of a miss. <see cref="TryMatchExact"/> below would not be sound without that fix.
/// </para>
/// <para>
/// Any <c>:FORMAT</c> or <c>:REGEX</c> segment is restricted from ever containing <c>/</c> - it is what
/// this class's own boundary-matching parses a route's literal separators by, so a captured value
/// containing one would be indistinguishable from the next path segment, and the wrong substring would be
/// attributed to each placeholder. This is not a rule about dates, or about formats specifically: it
/// applies identically to a regex segment, even though <c>/</c> has no special meaning in regex syntax
/// itself - a regex that explicitly wants to exclude it (<c>[^/]+</c>) is redundant anyway, since every
/// captured value is rejected outright if it contains one, regardless of what its regex says.
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
    /// A shallow cut's or a slug's last-placeholder capture is otherwise unbounded (see
    /// <see cref="TryMatchSlug"/>/<see cref="TryMatchPartial"/>'s own remarks for why).
    /// </summary>
    private static bool ContainsPathSeparator(string value) => value.Contains('/');

    /// <summary>
    /// The characters a placeholder's <c>:FORMAT</c> segment may use - letters, digits, <c>.</c>, <c>_</c>,
    /// <c>-</c>. Not a rule about dates specifically: it applies to every placeholder's format, whatever
    /// field it names, because the reason for it has nothing to do with what the field means - see the
    /// class remarks. This is also the one signal that disambiguates a lone <c>:</c>-delimited segment as
    /// a format rather than a regex: a real format string, by construction, can only ever match this
    /// pattern, so testing it first is what keeps every pre-existing <c>{name:FORMAT}</c> route classified
    /// exactly as it always was, with zero regression, before a segment that fails it is even offered to
    /// the regex compiler.
    /// </summary>
    private static readonly Regex FormatCharacters = new(@"^[a-zA-Z0-9_.-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Compiled placeholder regexes, keyed by pattern text - populated once per distinct pattern the first
    /// time <see cref="TryCompileRegex"/> sees it, then reused by every later placeholder/request that
    /// carries the same pattern rather than recompiling it. <see cref="RegexOptions.Compiled"/> pays for
    /// itself exactly because of this reuse - a route's regex is authored once by an admin and then matched
    /// against many requests.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new();

    /// <summary>
    /// How long a placeholder's regex is allowed to run against one captured value before it is treated as
    /// non-matching. The pattern's author (a trusted site admin) and the text it runs against (an
    /// untrusted visitor's request path) are different trust levels - this is a defensive bound against a
    /// pathological pattern, not a tuning knob for anything expected to be slow on its own.
    /// </summary>
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(250);

    private enum RouteTokenKind
    {
        Literal,
        Placeholder
    }

    /// <summary>
    /// One piece of a parsed route template - either a run of literal text matched verbatim, or a
    /// placeholder carrying its name and (optionally) its format and/or regex. The unit every method below
    /// actually works against once <see cref="TryParseTemplate"/> has run - ad hoc string slicing on the
    /// route text cannot safely locate a placeholder's own boundaries once its regex may itself contain
    /// <c>{</c>/<c>}</c> (see the class remarks).
    /// </summary>
    private sealed record RouteToken(RouteTokenKind Kind, string? Literal, string? Name, string? Format, string? RegexPattern)
    {
        public static RouteToken ForLiteral(string text) => new(RouteTokenKind.Literal, text, null, null, null);

        public static RouteToken ForPlaceholder(string name, string? format, string? regexPattern) =>
            new(RouteTokenKind.Placeholder, null, name, format, regexPattern);
    }

    private static bool IsNameStartChar(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    private static bool IsNameChar(char c) => IsNameStartChar(c) || (c is >= '0' and <= '9');

    /// <summary>
    /// Scans <paramref name="route"/> into an ordered list of literal and placeholder tokens, tracking
    /// brace depth once inside a placeholder's <c>:</c>-segment so an embedded, balanced <c>{</c>/<c>}</c>
    /// - a regex quantifier like <c>\d{4}</c>, above all - does not get mistaken for the placeholder's own
    /// closing brace; a backslash escapes the next character so an explicitly-escaped <c>\{</c>/<c>\}</c>
    /// inside a regex never perturbs the count either. Returns <see langword="false"/> for anything that
    /// does not parse cleanly: a stray, unmatched brace; an empty or malformed name; a <c>:</c>-segment
    /// that is neither a valid format nor a compilable regex (see <see cref="TryDisambiguate"/>). This
    /// single scan is also what <see cref="IsValid"/> relies on to reject a syntax error - there is no
    /// separate "anything left with a brace in it" pass the way there used to be, because a route this
    /// method fails to parse never had a well-formed placeholder to begin with.
    /// </summary>
    private static bool TryParseTemplate(string route, out List<RouteToken> tokens)
    {
        tokens = new List<RouteToken>();
        var literal = new StringBuilder();
        var i = 0;

        while (i < route.Length)
        {
            var c = route[i];

            if (c == '}')
            {
                return false; // a closing brace outside any placeholder can never be well-formed
            }

            if (c != '{')
            {
                literal.Append(c);
                i++;
                continue;
            }

            if (literal.Length > 0)
            {
                tokens.Add(RouteToken.ForLiteral(literal.ToString()));
                literal.Clear();
            }

            i++; // consume '{'
            var nameStart = i;
            if (i >= route.Length || !IsNameStartChar(route[i]))
            {
                return false;
            }
            i++;
            while (i < route.Length && IsNameChar(route[i]))
            {
                i++;
            }
            var name = route[nameStart..i];

            if (i >= route.Length)
            {
                return false; // unterminated
            }

            string? format = null;
            string? regexPattern = null;

            if (route[i] == '}')
            {
                i++; // bare {name}
            }
            else if (route[i] == ':')
            {
                i++; // consume ':'
                var innerStart = i;
                var depth = 0;
                var closed = false;

                while (i < route.Length)
                {
                    var ic = route[i];
                    if (ic == '\\' && i + 1 < route.Length)
                    {
                        i += 2;
                        continue;
                    }
                    if (ic == '{')
                    {
                        depth++;
                    }
                    else if (ic == '}')
                    {
                        if (depth == 0)
                        {
                            closed = true;
                            break;
                        }
                        depth--;
                    }
                    i++;
                }

                if (!closed)
                {
                    return false; // unterminated
                }

                var inner = route[innerStart..i];
                i++; // consume the closing '}'

                if (!TryDisambiguate(inner, out format, out regexPattern))
                {
                    return false;
                }
            }
            else
            {
                return false; // a name followed by neither ':' nor '}'
            }

            tokens.Add(RouteToken.ForPlaceholder(name, format, regexPattern));
        }

        if (literal.Length > 0)
        {
            tokens.Add(RouteToken.ForLiteral(literal.ToString()));
        }

        return true;
    }

    /// <summary>
    /// Classifies a placeholder's already brace-depth-isolated inner text (everything after the first
    /// <c>:</c>, up to its true closing brace) into <paramref name="format"/> and/or
    /// <paramref name="regexPattern"/> - the four <c>{name}</c>/<c>{name:FORMAT}</c>/<c>{name:REGEX}</c>/
    /// <c>{name:FORMAT:REGEX}</c> shapes collapse to exactly one question once the name itself is already
    /// stripped off: does this text split, at its first <c>:</c>, into a <see cref="FormatCharacters"/>-only
    /// prefix plus a remainder (then the prefix is the format and the remainder - free to contain further
    /// colons itself, e.g. a time regex - is the regex), or does the text as a whole satisfy
    /// <see cref="FormatCharacters"/> on its own (then it is a bare format, no colon involved at all), or
    /// else is it offered whole to <see cref="TryCompileRegex"/> as a regex. A text that is neither a valid
    /// format nor a compilable regex - or that contains <c>/</c> anywhere a regex would otherwise have been
    /// tried - fails placeholder parsing outright rather than silently degrading to "no constraint".
    /// </summary>
    private static bool TryDisambiguate(string rest, out string? format, out string? regexPattern)
    {
        format = null;
        regexPattern = null;

        var colon = rest.IndexOf(':');
        if (colon >= 0)
        {
            var candidateFormat = rest[..colon];
            if (candidateFormat.Length > 0 && FormatCharacters.IsMatch(candidateFormat))
            {
                if (!TryValidateRegexSegment(rest[(colon + 1)..], out regexPattern))
                {
                    return false;
                }
                format = candidateFormat;
                return true;
            }
        }

        if (FormatCharacters.IsMatch(rest))
        {
            format = rest;
            return true;
        }

        return TryValidateRegexSegment(rest, out regexPattern);
    }

    private static bool TryValidateRegexSegment(string text, out string? regexPattern)
    {
        regexPattern = null;

        // '/' is rejected here regardless of what the regex itself says (even one that explicitly excludes
        // it, like "[^/]+") - see the class remarks on why a captured value containing '/' is refused
        // unconditionally anyway, independent of any regex constraint.
        if (text.Length == 0 || text.Contains('/') || !TryCompileRegex(text, out _))
        {
            return false;
        }

        regexPattern = text;
        return true;
    }

    private static bool TryCompileRegex(string pattern, out Regex? regex)
    {
        try
        {
            regex = RegexCache.GetOrAdd(
                pattern,
                static p => new Regex(p, RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexMatchTimeout));
            return true;
        }
        catch (ArgumentException)
        {
            // Covers RegexParseException too - it derives from ArgumentException.
            regex = null;
            return false;
        }
    }

    /// <summary>
    /// The compiled form of a pattern that has already been through <see cref="TryCompileRegex"/> once -
    /// every <see cref="RouteToken.RegexPattern"/> that exists at all was only ever set after successfully
    /// compiling it during <see cref="TryParseTemplate"/>, so this is always already in
    /// <see cref="RegexCache"/> by the time anything calls it.
    /// </summary>
    private static Regex GetCompiledRegex(string pattern) => RegexCache[pattern];

    /// <summary>
    /// Whether <paramref name="route"/> is usable.
    /// <para>
    /// <c>{slug}</c> and <c>{slug?}</c> are mutually exclusive - a route asking for both at once has no
    /// coherent meaning; this is checked against the raw, uncanonicalized text, since canonicalizing first
    /// would erase the very distinction being checked. Every other <c>{...}</c> token must be a
    /// well-formed placeholder in one of the four supported shapes (see the class remarks) - whether
    /// <c>name</c> actually refers to a field the content has is not checked here, and cannot be: that
    /// requires reading a <c>ContentType</c>'s declared fields, which a route, on its own, has no way to
    /// reach. A route with no <c>{slug}</c>/<c>{slug?}</c> at all is valid - that is a page with nothing
    /// beneath it, not an error. See the class remarks for why this does not mirror Dignite.Cms's stricter
    /// rule.
    /// </para>
    /// <para>
    /// A placeholder's dedup key is its name alone, or <c>name:FORMAT</c> when a format is present - never
    /// its regex, even for a 3-segment placeholder - because two placeholders sharing that key would also
    /// collide at match time, in the single capture dictionary <see cref="TryMatchPartial"/>/
    /// <see cref="TryMatchExact"/> build, regardless of whether their regexes differ.
    /// </para>
    /// <para>
    /// <c>{slug}</c>/<c>{slug?}</c>, when present, may sit anywhere among the route's placeholders now - a
    /// placeholder after it is exactly as decorative as one before it (<see cref="TryMatchSlug"/> reads
    /// every placeholder back out by name, never by position - see its remarks). <see cref="TryMatchPartial"/>
    /// is the one place order still matters, and it owns that concern itself: it locates slug's own
    /// position and never cuts at or past it, rather than this method constraining what shape a valid
    /// route may take.
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

        if (!TryParseTemplate(Canonicalize(route), out var tokens))
        {
            return false;
        }

        var placeholders = tokens.Where(t => t.Kind == RouteTokenKind.Placeholder).ToList();

        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var placeholder in placeholders)
        {
            var key = placeholder.Format != null ? $"{placeholder.Name}:{placeholder.Format}" : placeholder.Name!;
            if (!seenKeys.Add(key))
            {
                return false;
            }
        }

        return true;
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
    /// Every placeholder collapses to just its resolved value - a regex constraint, when present, played
    /// its part at match time already (see <see cref="TryMatchSlug"/>/<see cref="TryMatchPartial"/>); it
    /// has nothing left to say about a value this method is <i>given</i> to write out, only about what may
    /// be read back in.
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

        if (!TryParseTemplate(canonical, out var tokens))
        {
            // Build is only ever called with a route that already passed IsValid at the point it was
            // stored (Page.SetRoute) - reaching an unparseable one here means the stored value was
            // corrupted after the fact, not a normal input this method needs to degrade gracefully for.
            throw new ArgumentException($"'{route}' is not a valid route template.", nameof(route));
        }

        var result = new StringBuilder();
        foreach (var token in tokens)
        {
            result.Append(token.Kind == RouteTokenKind.Literal ? token.Literal : valueResolver(token.Name!, token.Format));
        }

        return result.ToString();
    }

    /// <summary>
    /// The reverse of <see cref="Build"/>: reads the slug back out of a full request path, matched
    /// against the whole route as one anchored template - not a prefix plus a remainder.
    /// <para>
    /// Only the slug is returned, because only the slug identifies the content -
    /// <c>(PageId, CultureName, Slug)</c> is the unique constraint, so any other placeholder is decoration
    /// that has already done its job by matching, whatever text it actually captured (subject to its own
    /// regex, if it has one - see the class remarks). Requiring a date segment to agree with the content's
    /// stored publish time would mean an editor who reschedules a post breaks every link to it, so nothing
    /// here checks a decorative placeholder's captured text against anything beyond its own declared
    /// regex constraint, if any.
    /// </para>
    /// <para>
    /// The one thing that <i>is</i> always checked, regex or not: a captured value containing <c>/</c> is
    /// rejected unconditionally (see <see cref="Accept"/>). This matters most when nothing follows slug in
    /// the template - its capture is then unbounded, with no notion that a slug is exactly one segment,
    /// and a path with more segments than the route expects would otherwise still "match", with the extra
    /// segments folded into the slug. Rejecting a captured value that contains <c>/</c> is what keeps
    /// <c>/blog/{slug}</c> from matching <c>/blog/2026/07/my-post</c>.
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
        if (!TryParseTemplate(canonical, out var tokens) || !TryExtract(tokens, path, out var captures))
        {
            return false;
        }

        // Keyed by "slug" alone (bare, no format/regex) - a placeholder that merely happens to be *named*
        // slug but carries its own :FORMAT or :REGEX, e.g. {slug:someRegex}, keys as "slug:someRegex" and
        // is deliberately not recognized here either, a pre-existing quirk this only extends to the regex
        // case: HasSlug/IsSlugOptional already only ever recognize the bare {slug}/{slug?} tokens, so a
        // route carrying only the decorated form would never even reach this method with HasSlug true.
        if (captures.TryGetValue("slug", out var value))
        {
            slug = value;
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
    /// <c>publishTime:yyyy-MM = 2026-08</c>, one placeholder short of a slug. Only placeholders
    /// <i>before</i> slug are ever cut into like this - slug may now have decorative placeholders of its
    /// own after it (<see cref="IsValid"/> no longer requires it to be last), so this method locates
    /// slug's own position among the route's placeholders itself and never cuts at or past it, the same
    /// boundary <see cref="IsValid"/> used to enforce for it.
    /// <para>
    /// Tried from the deepest cut (every placeholder but slug filled) down to the shallowest (just the
    /// first) - never the full set, and never zero of them: a route made of one placeholder alone has
    /// nothing before it to cut at all, so this method can never answer for it (see
    /// <see cref="TryMatchExact"/> for the "every placeholder filled, but the route has no slug at all"
    /// case that gap otherwise leaves open) - each cut is itself matched the same all-or-nothing way
    /// <see cref="TryMatchSlug"/> matches a full route, just against a shorter prefix of it.
    /// </para>
    /// <para>
    /// A shallower cut's last placeholder is exactly as unbounded as <see cref="TryMatchSlug"/>'s slug is
    /// when nothing follows it in the template - rejecting a captured value that contains <c>/</c> is what
    /// keeps a shallow cut from swallowing path segments that actually belong to a deeper one, the same
    /// reasoning <see cref="TryMatchSlug"/> already applies to slug itself. A placeholder's own regex, if
    /// it has one, is checked the same way at every cut it appears filled in, not only at the deepest one.
    /// </para>
    /// <para>
    /// Callers decide which page's route to even try this against, and whether to try it at all - by the
    /// time this runs, <c>SiteRouteResolver</c> has already settled both which page a request belongs to
    /// and whether this candidate is even allowed a truncated reading (总体设计 §3.4); this only answers
    /// what the rest of the path means to that page once asked.
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
        if (!TryParseTemplate(canonical, out var tokens))
        {
            return false;
        }

        var placeholderTokenIndexes = new List<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind == RouteTokenKind.Placeholder)
            {
                placeholderTokenIndexes.Add(i);
            }
        }

        // The deepest cut this method may ever try stops right before slug - slug itself, and any
        // decorative placeholder now legal after it (IsValid no longer requires slug to be last), belong
        // to TryMatchSlug alone, never to a truncated reading. A route with no slug at all has nothing to
        // stop short of, so every placeholder is fair game, same as before slug support existed here.
        var upperCut = placeholderTokenIndexes.Count - 1;
        if (HasSlug(route))
        {
            upperCut = placeholderTokenIndexes.FindIndex(tokenIndex =>
            {
                var token = tokens[tokenIndex];
                return string.Equals(token.Name, "slug", StringComparison.OrdinalIgnoreCase) &&
                       token.Format == null && token.RegexPattern == null;
            });
        }

        // cut counts how many placeholders before slug are filled - from all but the last down to just the
        // first. cut == placeholderTokenIndexes.Count (nothing dropped) is TryMatchExact's territory, not
        // this method's; cut == 0 (nothing filled, i.e. the route's own bare address) is GetPath's.
        for (var cut = upperCut; cut >= 1; cut--)
        {
            var prefix = BuildTruncatedPrefix(tokens, placeholderTokenIndexes[cut]);

            if (!TryExtract(prefix, path, out var captures))
            {
                continue;
            }

            values = captures;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Matches <paramref name="path"/> against every one of <paramref name="route"/>'s placeholders with
    /// none dropped - the counterpart <see cref="TryMatchPartial"/> deliberately never attempts, and
    /// <see cref="TryMatchSlug"/> only attempts when the route has a <c>{slug}</c>/<c>{slug?}</c>
    /// placeholder at all, wherever it sits. A route built entirely of decorative placeholders -
    /// <c>/news/{publishTime:yyyy-MM}</c> on its own, say, with no slug at all - used to have no way to
    /// capture its own placeholder's value when a request named its exact address:
    /// <see cref="TryMatchSlug"/> does not apply (nothing is named slug), and
    /// <see cref="TryMatchPartial"/>'s cut loop requires at least two placeholders to run at all. This
    /// closes that gap.
    /// <para>
    /// Refuses outright - returning <see langword="false"/> without attempting anything - when
    /// <paramref name="route"/> has <see cref="HasSlug"/> (that request belongs to
    /// <see cref="TryMatchSlug"/>, which alone knows to look up content by the resulting slug) or carries
    /// no placeholder at all (there is nothing to capture; a bare address is <see cref="GetPath"/>'s and
    /// <c>SiteRouteResolver</c>'s "page itself" fallback's territory, not this one's).
    /// </para>
    /// <para>
    /// Matched case-sensitively, for the same reason <see cref="TryMatchSlug"/> is - see its own remarks.
    /// </para>
    /// </summary>
    /// <param name="route">The owning page's route.</param>
    /// <param name="path">The full, normalized request path.</param>
    public static bool TryMatchExact(string route, string path, out IReadOnlyDictionary<string, string> values)
    {
        values = EmptyValues;

        if (HasSlug(route))
        {
            return false;
        }

        var canonical = Canonicalize(route);
        if (!TryParseTemplate(canonical, out var tokens) || tokens.All(t => t.Kind != RouteTokenKind.Placeholder))
        {
            return false;
        }

        if (!TryExtract(tokens, path, out var captures))
        {
            return false;
        }

        values = captures;
        return true;
    }

    /// <summary>
    /// The tokens before (not including) the placeholder at <paramref name="cutTokenIndex"/>, with a
    /// trailing literal separator trimmed off the end - reproducing what slicing the canonical route
    /// string and calling <c>.TrimEnd('/')</c> on it used to do, now over tokens instead of characters, so
    /// a regex-bearing placeholder's own literal <c>{</c>/<c>}</c> can never be mistaken for a truncation
    /// point the way slicing raw text would risk. Trimming only ever removes a single trailing path
    /// separator, never any literal text before it - a shallow cut still requires whatever literal text
    /// actually separates it from the placeholder being dropped (e.g. the <c>"-archive"</c> in
    /// <c>{category}-archive/{publishTime}</c>) to still be present in the request.
    /// </summary>
    private static List<RouteToken> BuildTruncatedPrefix(List<RouteToken> tokens, int cutTokenIndex)
    {
        var prefix = new List<RouteToken>(tokens.GetRange(0, cutTokenIndex));

        if (prefix.Count > 0 && prefix[^1] is { Kind: RouteTokenKind.Literal, Literal: { } last } && last.EndsWith('/'))
        {
            var trimmed = last.TrimEnd('/');
            if (trimmed.Length == 0)
            {
                prefix.RemoveAt(prefix.Count - 1);
            }
            else
            {
                prefix[^1] = RouteToken.ForLiteral(trimmed);
            }
        }

        return prefix;
    }

    /// <summary>
    /// Matches <paramref name="path"/> against <paramref name="tokens"/> as one anchored template - the
    /// primitive every public matching method here reduces to. Walks literal tokens by
    /// <see cref="string.StartsWith(string, StringComparison)"/> (the first one) or
    /// <see cref="string.IndexOf(string, StringComparison)"/> (every one after), attributing whatever text
    /// sat before a found literal boundary to the placeholder token immediately preceding it; a
    /// placeholder with no literal token following it at all - the template's own last token - captures
    /// whatever remains of the input, unbounded. Two placeholder tokens with no literal between them is
    /// never actually producible by <see cref="TryParseTemplate"/> against a normalized route missing
    /// entirely a name-terminating boundary, but if it somehow occurred the earlier of the two would simply
    /// never get a capture - the same silent behavior the ABP algorithm this replaces always had.
    /// <para>
    /// Requires the input fully consumed when the template's own last token is literal, not a
    /// placeholder - the fix mentioned in the class remarks. Every captured value is additionally required
    /// to satisfy <see cref="Accept"/> (no <c>/</c>, and its own regex constraint if it has one) before it
    /// is accepted at all; any rejection - unmatched literal boundary, unconsumed trailing input, a failed
    /// <see cref="Accept"/> - fails the whole match, not just that one placeholder.
    /// </para>
    /// </summary>
    private static bool TryExtract(IReadOnlyList<RouteToken> tokens, string path, out Dictionary<string, string> captures)
    {
        captures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var remaining = path;
        RouteToken? pendingPlaceholder = null;

        foreach (var token in tokens)
        {
            if (token.Kind == RouteTokenKind.Placeholder)
            {
                // A second placeholder in a row with nothing between them leaves the first with no
                // boundary to be captured against at all - it is simply skipped, per the class remarks.
                pendingPlaceholder = token;
                continue;
            }

            var literal = token.Literal!;
            if (pendingPlaceholder == null)
            {
                if (!remaining.StartsWith(literal, StringComparison.Ordinal))
                {
                    return false;
                }
                remaining = remaining[literal.Length..];
                continue;
            }

            var boundary = remaining.IndexOf(literal, StringComparison.Ordinal);
            if (boundary < 0 || !Accept(pendingPlaceholder, remaining[..boundary], captures))
            {
                return false;
            }
            remaining = remaining[(boundary + literal.Length)..];
            pendingPlaceholder = null;
        }

        if (pendingPlaceholder != null)
        {
            return Accept(pendingPlaceholder, remaining, captures);
        }

        return remaining.Length == 0;
    }

    /// <summary>
    /// Whether <paramref name="value"/> - the text a boundary scan just captured for
    /// <paramref name="placeholder"/> - is acceptable at all, and if so, records it into
    /// <paramref name="captures"/> keyed by name alone, or <c>name:FORMAT</c> when a format is present -
    /// never by its regex, matching <see cref="IsValid"/>'s own dedup key, and matching exactly what every
    /// pre-existing route without a regex has always been keyed by (<see cref="TryMatchPartial"/>'s
    /// captured-values dictionary, in turn <c>RouteMatch.FilterValues</c>). Rejects a value containing
    /// <c>/</c> unconditionally - see the class remarks - and, when the placeholder carries a regex,
    /// rejects a value that fails it too, unanchored (<see cref="Regex.IsMatch(string)"/> as written; this
    /// never wraps a pattern in an implicit <c>^</c>/<c>$</c> the way a format was never validated against
    /// its own shape either - an author who wants a full-segment constraint writes the anchors themselves,
    /// same as this class's own examples do).
    /// </summary>
    private static bool Accept(RouteToken placeholder, string value, Dictionary<string, string> captures)
    {
        if (ContainsPathSeparator(value))
        {
            return false;
        }

        if (placeholder.RegexPattern != null && !GetCompiledRegex(placeholder.RegexPattern).IsMatch(value))
        {
            return false;
        }

        var key = placeholder.Format != null ? $"{placeholder.Name}:{placeholder.Format}" : placeholder.Name!;
        captures.Add(key, value); // .Add, not the indexer - a duplicate key here means IsValid let one through it should not have
        return true;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyValues = new Dictionary<string, string>();

    /// <summary>
    /// A few placeholders every route can use without anything else being configured first - surfaced so
    /// an admin UI or an MCP tool description can show examples instead of hard-coding its own copy. Not
    /// exhaustive: <see cref="IsValid"/> accepts <c>{name}</c>/<c>{name:FORMAT}</c>/<c>{name:REGEX}</c>/
    /// <c>{name:FORMAT:REGEX}</c> for any field name a content has, system property or <c>FlexFields</c>
    /// business field alike.
    /// </summary>
    public static IReadOnlyList<string> SupportedPlaceholders { get; } = new[]
    {
        SlugToken,
        OptionalSlugToken,
        "{publishTime:yyyy-MM}",
        "{publishTime:yyyy-MM:^\\d{4}-(0[1-9]|1[0-2])$}"
    };
}
