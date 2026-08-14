using System;
using System.Globalization;

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
    /// Whether <paramref name="format"/>/<paramref name="capturedValue"/> denote a whole period rather
    /// than one instant - and if so, that period as a half-open range: <paramref name="start"/> inclusive,
    /// <paramref name="endExclusive"/> exclusive. False whenever <paramref name="capturedValue"/> does not
    /// parse under <paramref name="format"/>, or <paramref name="format"/> names no date/time specifier
    /// this method recognizes. Never throws - a route filter value is untrusted input, and a bad one
    /// should degrade to "don't filter on this", not fail the whole render.
    /// </summary>
    public static bool TryGetRange(string format, string capturedValue, out DateTime start, out DateTime endExclusive)
    {
        endExclusive = default;

        if (!DateTime.TryParseExact(
                capturedValue, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
        {
            return false;
        }

        switch (FinestSpecifier(format))
        {
            case 's': endExclusive = start.AddSeconds(1); return true;
            case 'm': endExclusive = start.AddMinutes(1); return true;
            case 'h': endExclusive = start.AddHours(1); return true;
            case 'd': endExclusive = start.AddDays(1); return true;
            case 'M': endExclusive = start.AddMonths(1); return true;
            case 'y': endExclusive = start.AddYears(1); return true;
            default:
                start = default;
                return false;
        }
    }

    /// <summary>
    /// The smallest calendar/clock unit <paramref name="format"/> names, scanning outside
    /// <c>'...'</c>-quoted and <c>\</c>-escaped literal text the same way .NET's own custom date/time
    /// format parser does - a literal <c>m</c> inside a quoted label (e.g. <c>"'the month'"</c>) is not a
    /// minute specifier. Case-sensitive, as .NET's format specifiers are: <c>M</c> is month, <c>m</c> is
    /// minute; <c>H</c>/<c>h</c> are both "hour" for this purpose (24h vs 12h changes rendering, not
    /// granularity). Returns <c>'\0'</c> when nothing recognized appears at all.
    /// </summary>
    private static char FinestSpecifier(string format)
    {
        var sawYear = false;
        var sawMonth = false;
        var sawDay = false;
        var sawHour = false;
        var sawMinute = false;
        var sawSecond = false;

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

            switch (c)
            {
                case 's': sawSecond = true; break;
                case 'm': sawMinute = true; break;
                case 'H': case 'h': sawHour = true; break;
                case 'd': sawDay = true; break;
                case 'M': sawMonth = true; break;
                case 'y': sawYear = true; break;
            }
        }

        if (sawSecond) return 's';
        if (sawMinute) return 'm';
        if (sawHour) return 'h';
        if (sawDay) return 'd';
        if (sawMonth) return 'M';
        if (sawYear) return 'y';
        return '\0';
    }
}
