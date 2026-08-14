using System;
using System.Collections.Generic;
using System.Globalization;
using Dignite.Site.Pages;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// Splits a route match's raw placeholder captures (<c>RouteMatchDto.FilterValues</c>, 总体设计 §3.4) into
/// a publish-time window on <c>Content</c>'s own system field, and everything else, untouched. Done here,
/// upstream of <c>ContentListTagHelper</c>, so <c>PublishTime</c> - not a FlexFields field at all
/// (总体设计 §2.4) - never reaches <c>ContentListTagHelper.FieldFilters</c>, whose whole contract is
/// "these are FlexFields conditions."
/// </summary>
public static class SiteRenderFilterValueMapper
{
    /// <summary><c>Content</c>'s own system field (总体设计 §2.4) - the one entry this mapper carves out rather than forwarding.</summary>
    private const string PublishTimeFieldName = "PublishTime";

    public static (IReadOnlyDictionary<string, string> FieldFilters, DateTime? PublishedAfter, DateTime? PublishedBefore)
        Build(IDictionary<string, string> rawFilterValues)
    {
        var fieldFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DateTime? publishedAfter = null;
        DateTime? publishedBefore = null;

        foreach (var (key, capturedValue) in rawFilterValues)
        {
            // A placeholder's key is its name alone, or name:FORMAT when a format is present - never its
            // regex (PageRoute.cs's own Accept/IsValid remarks) - so splitting on the first ':' always
            // recovers (name, format) correctly.
            var colonIndex = key.IndexOf(':');
            var name = colonIndex < 0 ? key : key[..colonIndex];
            var format = colonIndex < 0 ? null : key[(colonIndex + 1)..];

            if (!string.Equals(name, PublishTimeFieldName, StringComparison.OrdinalIgnoreCase))
            {
                fieldFilters[key] = capturedValue;
                continue;
            }

            // A date-shaped FORMAT denotes a whole period (e.g. yyyy-MM = one month), not one instant - see
            // RoutePlaceholderDateFormat. Falls back to an exact-instant match when there is no FORMAT at
            // all (a bare {publishTime} placeholder - legal, if unusual) by pinning both bounds to the same
            // parsed value. A capture that parses as neither is silently dropped, never an error - a route
            // filter value is untrusted input and must never fail the whole render.
            if (format != null &&
                RoutePlaceholderDateFormat.TryGetRange(format, capturedValue, out var start, out var endExclusive))
            {
                publishedAfter = start;
                // PublishedBefore is a closed interval (PublishTime <= value); endExclusive is this
                // period's open end, so the latest instant still inside it is one tick earlier.
                publishedBefore = endExclusive.AddTicks(-1);
            }
            else if (format == null && DateTime.TryParse(
                         capturedValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            {
                publishedAfter = exact;
                publishedBefore = exact;
            }
        }

        return (fieldFilters, publishedAfter, publishedBefore);
    }
}
