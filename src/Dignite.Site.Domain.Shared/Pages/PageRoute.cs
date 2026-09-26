using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
/// The slug itself takes a REGEX too - <c>{slug:REGEX}</c>, a mandatory slug a request's slug segment
/// must also match: <c>/{slug:^(privacy-policy|terms-of-service)$}</c> gives a few contents their own
/// root-level addresses without the page answering for every other root-level path the way a bare
/// <c>/{slug}</c> would. The REGEX is also what a content's own slug must match when it is saved
/// (<see cref="IsSlugAllowed"/>) - otherwise the site would emit a URL for it that it then refuses to
/// route. A slug takes no FORMAT (it is text, and a format only ever formats an <c>IFormattable</c>), and
/// no optional marker next to a REGEX - see <see cref="AreOptionalPlaceholdersWellFormed"/>.
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
/// Any placeholder may also be marked optional with a <c>?</c> right after its name - <c>{name?}</c>,
/// <c>{name?:FORMAT}</c>, <c>{name?:REGEX}</c>, <c>{name?:FORMAT:REGEX}</c> - and a request path may then
/// leave it out altogether, together with the <c>/</c> that introduces its segment:
/// <c>/news/{category?:^(news|tutorials)$}/{publishTime?:yyyy:^\d{4}$}/{publishTime?:MM:^(0[1-9]|1[0-2])$}</c>
/// answers <c>/news/tutorials</c>, <c>/news/2026</c> and <c>/news/2026/08</c> alike. Right after the name,
/// not at the end of the placeholder: <c>?</c> is never a name character, so there it cannot be mistaken
/// for anything else, whereas at the end of a FORMAT or REGEX segment it would read as regex syntax.
/// <c>{slug?}</c> is the one older instance of this shape and keeps its own, different meaning - an empty
/// slug, served at the page's own address - so it is canonicalized to <c>{slug}</c> before any of this
/// applies. See <see cref="AreOptionalPlaceholdersWellFormed"/> for the rules an optional placeholder has
/// to follow, and <see cref="ExpandOptionalVariants"/> for how one is matched.
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

    /// <summary>
    /// How many optional placeholders one route may carry. Each one doubles the concrete templates a
    /// request is tried against (<see cref="ExpandOptionalVariants"/>) - four is sixteen, already more
    /// than any real archive/category URL scheme needs.
    /// </summary>
    private const int MaxOptionalPlaceholders = 4;

    private enum RouteTokenKind
    {
        Literal,
        Placeholder
    }

    /// <summary>
    /// One piece of a parsed route template - either a run of literal text matched verbatim, or a
    /// placeholder carrying its name, (optionally) its format and/or regex, and whether it is optional. The
    /// unit every method below actually works against once <see cref="TryParseTemplate"/> has run - ad hoc
    /// string slicing on the route text cannot safely locate a placeholder's own boundaries once its regex
    /// may itself contain <c>{</c>/<c>}</c> (see the class remarks).
    /// </summary>
    private sealed record RouteToken(
        RouteTokenKind Kind, string? Literal, string? Name, string? Format, string? RegexPattern, bool IsOptional)
    {
        public static RouteToken ForLiteral(string text) => new(RouteTokenKind.Literal, text, null, null, null, false);

        public static RouteToken ForPlaceholder(string name, string? format, string? regexPattern, bool isOptional) =>
            new(RouteTokenKind.Placeholder, null, name, format, regexPattern, isOptional);
    }

    /// <summary>
    /// Whether <paramref name="token"/> is the route's slug - <c>{slug}</c> or <c>{slug:REGEX}</c> once
    /// <see cref="Canonicalize"/> has run, never one carrying a FORMAT (<see cref="IsValid"/> rejects that
    /// shape outright, see the class remarks).
    /// </summary>
    private static bool IsSlugPlaceholder(RouteToken token) =>
        token is { Kind: RouteTokenKind.Placeholder, Format: null } &&
        string.Equals(token.Name, "slug", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A placeholder name starts with a letter or digit and continues with letters, digits, <c>_</c> or
    /// <c>-</c> - every <see cref="IdentifierName"/> a <c>Field</c> can be named (<c>my_field</c>,
    /// <c>post-article</c>, <c>2026-report</c>), plus uppercase for <c>Content</c>'s own camel-cased system
    /// properties (<c>publishTime</c>). A route must be able to reference any field a content can have
    /// (总体设计 §2.4), so this set may never be narrower than <see cref="IdentifierName.Pattern"/>.
    /// Neither <c>_</c> nor <c>-</c> is ambiguous here: a name only ever sits between <c>{</c> and the
    /// first <c>}</c>/<c>:</c>, never next to a route's literal text, so a <c>-</c> separating two
    /// placeholders in <c>{slug}-{title}</c> is outside both names and unaffected.
    /// </summary>
    private static bool IsNameStartChar(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');

    private static bool IsNameChar(char c) => IsNameStartChar(c) || c is '_' or '-';

    /// <summary>
    /// Scans <paramref name="route"/> into an ordered list of literal and placeholder tokens, tracking
    /// brace depth once inside a placeholder's <c>:</c>-segment so an embedded, balanced <c>{</c>/<c>}</c>
    /// - a regex quantifier like <c>\d{4}</c>, above all - does not get mistaken for the placeholder's own
    /// closing brace; a backslash escapes the next character so an explicitly-escaped <c>\{</c>/<c>\}</c>
    /// inside a regex never perturbs the count either. Returns <see langword="false"/> for anything that
    /// does not parse cleanly: a stray, unmatched brace; an empty or malformed name; a <c>:</c>-segment
    /// that is neither a valid format nor a compilable regex (see <see cref="TryDisambiguate"/>). A
    /// <c>?</c> right after a name marks the placeholder optional - whether it is allowed to be where it
    /// is, is <see cref="AreOptionalPlaceholdersWellFormed"/>'s question, not this scan's. This
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

            var isOptional = false;
            if (i < route.Length && route[i] == '?')
            {
                isOptional = true;
                i++;
            }

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
                return false; // a name (and its '?', if any) followed by neither ':' nor '}'
            }

            tokens.Add(RouteToken.ForPlaceholder(name, format, regexPattern, isOptional));
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
    /// <paramref name="route"/> parsed into tokens the way every public method here needs it:
    /// <c>{slug?}</c> canonicalized to <c>{slug}</c> first, then rejected exactly like a syntax error when
    /// an optional placeholder breaks one of <see cref="AreOptionalPlaceholdersWellFormed"/>'s rules - which
    /// <see cref="Build"/> and every matching method rely on without checking them again.
    /// </summary>
    private static bool TryParseRoute(string route, out List<RouteToken> tokens)
    {
        return TryParseTemplate(Canonicalize(route), out tokens) && AreOptionalPlaceholdersWellFormed(tokens);
    }

    /// <summary>
    /// Whether every optional placeholder in <paramref name="tokens"/> is one this class can actually match:
    /// <list type="number">
    /// <item>It fills a whole path segment on its own - a literal ending in <c>/</c> right before it, and
    /// either nothing or a literal starting with <c>/</c> right after it. Leaving it out removes exactly that
    /// segment; there is no equally obvious answer for what to remove from <c>post-{category?}</c>.</item>
    /// <item>Every optional placeholder but the last one carries a REGEX. Without one it would claim any
    /// segment offered to it, and a later optional placeholder could never be reached in its place -
    /// <c>/news/{a?}/{b?}</c> would answer <c>/news/x</c> with <c>a</c> every time (see
    /// <see cref="ExpandOptionalVariants"/>'s leftmost-first order). The last one has nothing after it to
    /// shadow, which is also why <c>/blog/{category?}/{slug}</c> needs no REGEX: it is the only one.</item>
    /// <item>There are at most <see cref="MaxOptionalPlaceholders"/> of them.</item>
    /// </list>
    /// A placeholder named <c>slug</c> is never optional this way. <c>{slug?}</c> itself is canonicalized
    /// away before this runs, so one still marked optional here is a decorated form such as
    /// <c>{slug?:REGEX}</c>, which has no coherent meaning next to <c>{slug?}</c>'s own.
    /// </summary>
    private static bool AreOptionalPlaceholdersWellFormed(List<RouteToken> tokens)
    {
        var optionalIndexes = GetOptionalPlaceholderIndexes(tokens);
        if (optionalIndexes.Count > MaxOptionalPlaceholders)
        {
            return false;
        }

        for (var n = 0; n < optionalIndexes.Count; n++)
        {
            var index = optionalIndexes[n];
            var token = tokens[index];

            if (string.Equals(token.Name, "slug", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (index == 0 || tokens[index - 1] is not { Kind: RouteTokenKind.Literal, Literal: { } before } ||
                !before.EndsWith('/'))
            {
                return false;
            }

            if (index + 1 < tokens.Count &&
                (tokens[index + 1] is not { Kind: RouteTokenKind.Literal, Literal: { } after } || !after.StartsWith('/')))
            {
                return false;
            }

            if (n < optionalIndexes.Count - 1 && token.RegexPattern == null)
            {
                return false;
            }
        }

        return true;
    }

    private static List<int> GetOptionalPlaceholderIndexes(List<RouteToken> tokens)
    {
        var indexes = new List<int>();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is { Kind: RouteTokenKind.Placeholder, IsOptional: true })
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }

    /// <summary>
    /// Every concrete template <paramref name="tokens"/> stands for once each of its optional placeholders
    /// is either kept or left out - 2^n of them for n optional placeholders, and just the tokens as they
    /// are when there are none. Ordered most placeholders kept first, then, among those keeping the same
    /// number, the one keeping the leftmost first - the order every matching method tries them in, taking
    /// the first that fits. Since an optional placeholder is always a whole segment of its own, templates
    /// keeping a different number of them never fit the same path at all; the leftmost-first order is what
    /// settles the rest. <c>/news/2026</c> against the class remarks' example tries <c>category</c> before
    /// <c>publishTime</c>, and it is <c>category</c>'s own REGEX that turns it down - which is why
    /// <see cref="AreOptionalPlaceholdersWellFormed"/> requires one on every optional placeholder but the
    /// last.
    /// </summary>
    private static IEnumerable<List<RouteToken>> ExpandOptionalVariants(List<RouteToken> tokens)
    {
        var optionalIndexes = GetOptionalPlaceholderIndexes(tokens);
        if (optionalIndexes.Count == 0)
        {
            yield return tokens;
            yield break;
        }

        var count = optionalIndexes.Count;

        // Bit (count - 1 - n) set means the n-th optional placeholder is kept, so among masks keeping the
        // same number of them, a larger one keeps placeholders further to the left.
        var masks = Enumerable.Range(0, 1 << count)
            .OrderByDescending(mask => BitOperations.PopCount((uint)mask))
            .ThenByDescending(mask => mask);

        foreach (var mask in masks)
        {
            var omitted = new HashSet<int>();
            for (var n = 0; n < count; n++)
            {
                if ((mask & (1 << (count - 1 - n))) == 0)
                {
                    omitted.Add(optionalIndexes[n]);
                }
            }

            yield return OmitPlaceholders(tokens, omitted);
        }
    }

    /// <summary>
    /// <paramref name="tokens"/> with the placeholders at <paramref name="omittedTokenIndexes"/> left out,
    /// each together with the <c>/</c> that introduced its segment
    /// (<see cref="AreOptionalPlaceholdersWellFormed"/> guarantees the literal right before one ends in it),
    /// and every two literals that end up adjacent merged into one - <see cref="TryExtract"/> locates a
    /// literal boundary by the whole literal's text at once, not piece by piece. Leaving out every segment
    /// of a route whose own address is the root leaves nothing at all, which is the root itself, <c>/</c>.
    /// </summary>
    private static List<RouteToken> OmitPlaceholders(List<RouteToken> tokens, HashSet<int> omittedTokenIndexes)
    {
        var result = new List<RouteToken>();

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (omittedTokenIndexes.Contains(i))
            {
                result[^1] = RouteToken.ForLiteral(result[^1].Literal![..^1]);
                continue;
            }

            if (token.Kind == RouteTokenKind.Literal && result.Count > 0 && result[^1].Kind == RouteTokenKind.Literal)
            {
                result[^1] = RouteToken.ForLiteral(result[^1].Literal + token.Literal);
                continue;
            }

            result.Add(token);
        }

        result.RemoveAll(t => t is { Kind: RouteTokenKind.Literal, Literal.Length: 0 });
        if (result.Count == 0)
        {
            result.Add(RouteToken.ForLiteral("/"));
        }

        return result;
    }

    /// <summary>
    /// <see cref="TryExtract"/> against each of <paramref name="tokens"/>'s optional-placeholder variants
    /// in turn (<see cref="ExpandOptionalVariants"/>), the first that fits winning - which, for a template
    /// with no optional placeholder, is exactly <see cref="TryExtract"/> itself, the only variant there is.
    /// </summary>
    private static bool TryExtractAny(List<RouteToken> tokens, string path, out Dictionary<string, string> captures)
    {
        foreach (var variant in ExpandOptionalVariants(tokens))
        {
            if (TryExtract(variant, path, out captures))
            {
                return true;
            }
        }

        captures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return false;
    }

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
    /// rule. An optional placeholder additionally has to follow <see cref="AreOptionalPlaceholdersWellFormed"/>'s
    /// rules.
    /// </para>
    /// <para>
    /// A placeholder named <c>slug</c> never carries a FORMAT. Note that the lone-<c>:</c> disambiguation
    /// applies to it like to any other name: <c>{slug:about}</c> reads as a FORMAT and is rejected here, so
    /// a slug REGEX has to look like one - <c>{slug:^about$}</c>, which also anchors it.
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

        if (!TryParseRoute(route, out var tokens))
        {
            return false;
        }

        var placeholders = tokens.Where(t => t.Kind == RouteTokenKind.Placeholder).ToList();

        // {slug:FORMAT} would be a placeholder named slug that is not the slug - one the content editor,
        // the router and the save-time check would each have to decide about separately.
        if (placeholders.Any(t => string.Equals(t.Name, "slug", StringComparison.OrdinalIgnoreCase) && t.Format != null))
        {
            return false;
        }

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
    /// Whether <paramref name="route"/> is the site's home route - whether its own address
    /// (<see cref="GetPath"/>) is exactly <c>/</c>, the site root, and it does not require a slug. Not a
    /// literal comparison against <c>"/"</c>: a route like <c>/{slug?}</c> or <c>/{category}/{slug?}</c>
    /// also has no literal segment before its first placeholder, so its own address is the root exactly
    /// the same way a bare <c>/</c> is. Whether a page is the home page is therefore never a stored flag
    /// either, the same reasoning the class remarks already give for whether a page has content at all.
    /// <para>
    /// A route that requires a slug - <c>/{slug}</c>, <c>/{slug:^(privacy-policy|terms-of-service)$}</c> -
    /// shares that root address but is not a home route: every content beneath it has an address of its
    /// own (<c>/privacy-policy</c>), and none of them is ever served at <c>/</c> itself. Such a page exists
    /// to give contents root-level addresses, not to be the page a visitor lands on. <c>{slug?}</c> is
    /// different exactly there - its empty-slug content is served at the page's own address, the root.
    /// </para>
    /// </summary>
    public static bool IsHomeRoute(string route) =>
        GetPath(route) == "/" && (!HasSlug(route) || IsSlugOptional(route));

    /// <summary>
    /// Whether <paramref name="route"/> carries any placeholder at all - a template rather than a plain
    /// literal path. This is the tie-break a literal route wins when it shares its own address with a
    /// template (总体设计 §3.4): a route this returns false for is already its own address, never a
    /// derived one, so it is always the more specific match.
    /// </summary>
    public static bool IsTemplate(string route) => route.IndexOf('{') >= 0;

    /// <summary>
    /// Whether <paramref name="route"/> carries a <c>{slug}</c>, <c>{slug?}</c> or <c>{slug:REGEX}</c> -
    /// whether the page has content beneath it.
    /// </summary>
    public static bool HasSlug(string route)
    {
        if (route.Contains(SlugToken, StringComparison.OrdinalIgnoreCase) ||
            route.Contains(OptionalSlugToken, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryParseRoute(route, out var tokens) && tokens.Any(IsSlugPlaceholder);
    }

    /// <summary>
    /// Whether <paramref name="slug"/> may be stored beneath <paramref name="route"/> as far as the slug's
    /// own REGEX is concerned - true when the route's slug has none, and for an empty slug, whose rules are
    /// <see cref="HasSlug"/>'s and <see cref="IsSlugOptional"/>'s. Matched exactly as a request's slug
    /// segment is (<see cref="Accept"/>), so what this lets through is what <see cref="TryMatchSlug"/> will
    /// route back: a content saved with a slug its route's REGEX turns down would get a URL in the sitemap,
    /// canonical and hreflang that the site itself answers with 404.
    /// </summary>
    public static bool IsSlugAllowed(string route, string slug)
    {
        if (slug.Length == 0 || !TryParseRoute(route, out var tokens))
        {
            return true;
        }

        var slugToken = tokens.FirstOrDefault(IsSlugPlaceholder);
        return slugToken?.RegexPattern == null || GetCompiledRegex(slugToken.RegexPattern).IsMatch(slug);
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
        return Build(route, (name, format, _) => valueResolver(name, format));
    }

    /// <summary>
    /// <see cref="Build(string, Func{string, string, string})"/>, for a resolver that also needs to know
    /// whether the placeholder it is resolving is optional - a field a content simply does not have is a
    /// configuration mistake for a required placeholder, but an ordinary, empty value for an optional one
    /// (<see cref="Page.BuildContentPath"/>). An optional placeholder resolved to null or empty is left out
    /// of the URL together with the <c>/</c> that introduces its segment, exactly the way a request is
    /// allowed to leave it out (see the class remarks): <c>/blog/{category?}/{slug}</c> builds
    /// <c>/blog/my-post</c> for a content with no category. A required one resolved to null or empty is
    /// written out empty, as it always was.
    /// </summary>
    /// <param name="route">The owning page's route.</param>
    /// <param name="valueResolver">Resolves one placeholder from its name, optional format, and whether it is optional.</param>
    public static string Build(string route, Func<string, string?, bool, string?> valueResolver)
    {
        if (!TryParseRoute(route, out var tokens))
        {
            // Build is only ever called with a route that already passed IsValid at the point it was
            // stored (Page.SetRoute) - reaching an unparseable one here means the stored value was
            // corrupted after the fact, not a normal input this method needs to degrade gracefully for.
            throw new ArgumentException($"'{route}' is not a valid route template.", nameof(route));
        }

        var result = new StringBuilder();
        foreach (var token in tokens)
        {
            if (token.Kind == RouteTokenKind.Literal)
            {
                result.Append(token.Literal);
                continue;
            }

            var value = valueResolver(token.Name!, token.Format, token.IsOptional);
            if (token.IsOptional && string.IsNullOrEmpty(value))
            {
                // AreOptionalPlaceholdersWellFormed guarantees what was just written is a literal ending in
                // the '/' that introduces this placeholder's segment - it goes along with it.
                result.Length--;
                continue;
            }

            result.Append(value);
        }

        return result.Length == 0 ? "/" : result.ToString();
    }

    /// <summary>
    /// The reverse of <see cref="TryMatchExact"/>: the path <paramref name="capturedValues"/> were read
    /// from, rebuilt from the route rather than remembered from the request - <c>/news/2026/08</c> again,
    /// from <c>{"publishTime:yyyy": "2026", "publishTime:MM": "08"}</c> and a route with those two
    /// placeholders. What a declared filtered view's canonical URL is derived from (总体设计 §5.3: canonical
    /// is computed from the route's structure). Values are looked up by the same key the matching methods
    /// capture them under - name, or <c>name:FORMAT</c> - and an optional placeholder with no value is left
    /// out, exactly as <see cref="Build(string, Func{string, string, bool, string})"/> leaves it out.
    /// </summary>
    /// <exception cref="ArgumentException">A required placeholder has no value in <paramref name="capturedValues"/> - they were not captured from this route.</exception>
    public static string BuildFromCapturedValues(string route, IReadOnlyDictionary<string, string> capturedValues)
    {
        return Build(route, (name, format, isOptional) =>
        {
            var key = GetCaptureKey(name, format);
            if (capturedValues.TryGetValue(key, out var value))
            {
                return value;
            }

            if (isOptional)
            {
                return null;
            }

            throw new ArgumentException($"No captured value for '{key}' in route '{route}'.", nameof(capturedValues));
        });
    }

    /// <summary>
    /// The key a placeholder's captured value is stored under - its name alone, or <c>name:FORMAT</c> when
    /// a format is present, never its regex (see <see cref="Accept"/> and <see cref="IsValid"/>).
    /// </summary>
    private static string GetCaptureKey(string name, string? format) => format != null ? $"{name}:{format}" : name;

    /// <summary>
    /// The reverse of <see cref="Build"/>: reads the slug back out of a full request path, matched
    /// against the whole route as one anchored template - not a prefix plus a remainder - or, when the
    /// route has optional placeholders, against the first of its variants that fits
    /// (<see cref="ExpandOptionalVariants"/>).
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

        if (!TryParseRoute(route, out var tokens) || !TryExtractAny(tokens, path, out var captures))
        {
            return false;
        }

        // Keyed by "slug" alone - a capture key never includes a regex (GetCaptureKey), so {slug:REGEX}
        // lands here the same as {slug}, its regex already applied by Accept. {slug:FORMAT} would key as
        // "slug:FORMAT", but IsValid never lets that shape be stored.
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
    /// An optional placeholder inside a cut may still be left out, the same way it may be in a full match
    /// (<see cref="ExpandOptionalVariants"/>) - but never so that nothing is left beyond the route's own
    /// bare address: that address is <see cref="GetPath"/>'s and <c>SiteRouteResolver</c>'s "page itself"
    /// fallback's, the only one that also knows to look for a content with an empty slug there.
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

        if (!TryParseRoute(route, out var tokens) || path == GetPath(route))
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
            upperCut = placeholderTokenIndexes.FindIndex(tokenIndex => IsSlugPlaceholder(tokens[tokenIndex]));
        }

        // cut counts how many placeholders before slug are filled - from all but the last down to just the
        // first. cut == placeholderTokenIndexes.Count (nothing dropped) is TryMatchExact's territory, not
        // this method's; cut == 0 (nothing filled, i.e. the route's own bare address) is GetPath's.
        for (var cut = upperCut; cut >= 1; cut--)
        {
            var prefix = BuildTruncatedPrefix(tokens, placeholderTokenIndexes[cut]);

            if (!TryExtractAny(prefix, path, out var captures))
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
    /// "None dropped" means none the route did not itself declare optional: an optional placeholder may be
    /// left out here, and that is the whole point of one - <c>/news/{publishTime:yyyy}/{publishTime?:MM}</c>
    /// answers <c>/news/2026</c> here, as a full match, rather than as a truncated one only
    /// <see cref="TryMatchPartial"/> could offer and <c>SiteRouteResolver</c> withholds the moment another
    /// page shares its address. Declared, not guessed at, so nothing needs withholding. The one thing still
    /// refused is the route's own bare address itself, even when every placeholder is optional and leaving
    /// them all out would fit it - for the same reason given above for a route with no placeholder at all;
    /// letting a template claim it here would also let it beat a literal page sharing that address, the
    /// tie-break 总体设计 §3.4 gives the literal one.
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

        if (!TryParseRoute(route, out var tokens) || tokens.All(t => t.Kind != RouteTokenKind.Placeholder) ||
            path == GetPath(route))
        {
            return false;
        }

        if (!TryExtractAny(tokens, path, out var captures))
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

        var key = GetCaptureKey(placeholder.Name!, placeholder.Format);
        captures.Add(key, value); // .Add, not the indexer - a duplicate key here means IsValid let one through it should not have
        return true;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyValues = new Dictionary<string, string>();

    /// <summary>
    /// A few placeholders every route can use without anything else being configured first - surfaced so
    /// an admin UI or an MCP tool description can show examples instead of hard-coding its own copy. Not
    /// exhaustive: <see cref="IsValid"/> accepts <c>{name}</c>/<c>{name:FORMAT}</c>/<c>{name:REGEX}</c>/
    /// <c>{name:FORMAT:REGEX}</c> - each optionally marked <c>{name?...}</c> - for any field name a content
    /// has, system property or <c>FlexFields</c> business field alike.
    /// </summary>
    public static IReadOnlyList<string> SupportedPlaceholders { get; } = new[]
    {
        SlugToken,
        OptionalSlugToken,
        "{slug:^(privacy-policy|terms-of-service)$}",
        "{publishTime:yyyy-MM}",
        "{publishTime:yyyy-MM:^\\d{4}-(0[1-9]|1[0-2])$}",
        "{publishTime?:MM:^(0[1-9]|1[0-2])$}"
    };
}
