using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dignite.Site.Pages;

/// <summary>
/// What time period a route placeholder's FORMAT (总体设计 §3.3) denotes, for filtering a list by a
/// partially-matched placeholder value (总体设计 §3.4's "按月归档" example: <c>{publishTime:yyyy-MM}</c>
/// captured as <c>"2026-08"</c> means the whole month of August 2026, not one specific instant). Pure and
/// FlexFields/Content-agnostic - it only ever sees the FORMAT string and the text a route captured, the
/// same two pieces of information <see cref="PageRoute"/> itself already works with.
/// </summary>
public static class RoutePlaceholderDateFormat
{
    /// <summary>
    /// Every calendar/clock unit <see cref="ScanUnits"/> recognizes, coarsest first - the order a period
    /// has to be spelled out in, without gaps, for it to denote one (see the multi-part overload of
    /// <c>TryGetRange</c>).
    /// </summary>
    private const string UnitsCoarseToFine = "yMdhms";

    /// <summary>
    /// Whether <paramref name="format"/>/<paramref name="capturedValue"/> denote a whole period rather
    /// than one instant - and if so, that period as a half-open range: <paramref name="start"/> inclusive,
    /// <paramref name="endExclusive"/> exclusive. The single-placeholder case of the multi-part overload -
    /// see its remarks for every rule this follows too.
    /// </summary>
    public static bool TryGetRange(string format, string capturedValue, out DateTime start, out DateTime endExclusive)
    {
        return TryGetRange(new[] { (format, capturedValue) }, out start, out endExclusive);
    }

    /// <summary>
    /// The period several placeholders naming the same field denote together - <c>{publishTime:yyyy}</c>
    /// and <c>{publishTime:MM}</c> captured as <c>"2026"</c> and <c>"08"</c> are one month, August 2026,
    /// not a year and a month to be judged one after the other. Parsed as one date, never piece by piece:
    /// a lone <c>MM</c> handed to <c>DateTime.TryParseExact</c> silently takes the <i>current</i> year, so
    /// judging each part on its own made <c>/2025/08</c> filter on August of whatever year it is now.
    /// <para>
    /// False whenever the parts do not parse together, or name no date/time specifier this method
    /// recognizes, or leave a gap between the year and the finest unit they name - a month with no year,
    /// a day with no month. That incomplete date would otherwise be filled in by the parser's own defaults
    /// (the current year, January), which is exactly the silent guess this refuses to make. Never throws -
    /// a route filter value is untrusted input, and a bad one should degrade to "don't filter on this",
    /// not fail the whole render.
    /// </para>
    /// </summary>
    /// <param name="parts">Each placeholder's FORMAT and captured text, in any order.</param>
    public static bool TryGetRange(
        IEnumerable<(string Format, string CapturedValue)> parts,
        out DateTime start,
        out DateTime endExclusive)
    {
        start = default;
        endExclusive = default;

        var list = parts.ToList();
        if (list.Count == 0)
        {
            return false;
        }

        // Joined into one format/value pair so the parser sees every unit at once. '/' is safe as the
        // joint on both sides: a route FORMAT can never contain one, and a captured value never does either
        // (PageRoute rejects it). Quoted in the format so it stays a literal, not the culture's date separator.
        var format = string.Join("'/'", list.Select(p => p.Format));
        var value = string.Join("/", list.Select(p => p.CapturedValue));

        var units = ScanUnits(format);
        var finest = UnitsCoarseToFine.Length - 1;
        while (finest >= 0 && !units[finest])
        {
            finest--;
        }

        if (finest < 0 || units.Take(finest).Any(seen => !seen))
        {
            return false;
        }

        if (!DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        start = parsed;
        endExclusive = UnitsCoarseToFine[finest] switch
        {
            's' => parsed.AddSeconds(1),
            'm' => parsed.AddMinutes(1),
            'h' => parsed.AddHours(1),
            'd' => parsed.AddDays(1),
            'M' => parsed.AddMonths(1),
            _ => parsed.AddYears(1)
        };
        return true;
    }

    /// <summary>
    /// Whether <paramref name="format"/> names any calendar/clock unit at all - i.e. whether it is a date
    /// format, whose captured text either forms a period (<c>TryGetRange</c>) or is unusable as a filter,
    /// rather than some other kind of format (a number's <c>0000</c>, say) this class has nothing to say
    /// about.
    /// </summary>
    public static bool IsDateFormat(string format) => ScanUnits(format).Any(seen => seen);

    /// <summary>
    /// Which of <see cref="UnitsCoarseToFine"/> <paramref name="format"/> names, scanning outside
    /// <c>'...'</c>-quoted and <c>\</c>-escaped literal text the same way .NET's own custom date/time
    /// format parser does - a literal <c>m</c> inside a quoted label (e.g. <c>"'the month'"</c>) is not a
    /// minute specifier. Case-sensitive, as .NET's format specifiers are: <c>M</c> is month, <c>m</c> is
    /// minute; <c>H</c>/<c>h</c> are both "hour" for this purpose (24h vs 12h changes rendering, not
    /// granularity).
    /// </summary>
    private static bool[] ScanUnits(string format)
    {
        var seen = new bool[UnitsCoarseToFine.Length];

        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];

            if (c == '\\')
            {
                i++; // skip the escaped character itself, whatever it is
                continue;
            }

            if (c == '\'')
            {
                var closing = format.IndexOf('\'', i + 1);
                i = closing < 0 ? format.Length : closing; // unterminated quote: rest of the string is literal
                continue;
            }

            var unit = UnitsCoarseToFine.IndexOf(c == 'H' ? 'h' : c);
            if (unit >= 0)
            {
                seen[unit] = true;
            }
        }

        return seen;
    }
}
