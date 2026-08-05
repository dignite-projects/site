using System;
using Dignite.Sites.Seo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Dignite.Sites.Public.Seo;

/// <summary>
/// Matches a request path whose final segment is one of the feed file names.
/// <para>
/// The feed routes have to be catch-alls: page routes are runtime data of any depth, and a route template
/// may not place a literal segment <i>after</i> a catch-all parameter, so <c>{page}/feed.xml</c> cannot be
/// expressed. A bare catch-all would swallow the entire site, so it is narrowed by this constraint
/// instead.
/// </para>
/// <para>
/// A named constraint rather than an inline <c>regex(...)</c>: the parentheses in a regex confuse the
/// route-template analyzer into reporting a malformed template, and - more usefully - this way the file
/// names come from <see cref="FeedConsts"/> rather than being restated as a pattern that could drift out
/// of step with them.
/// </para>
/// </summary>
public class FeedPathRouteConstraint : IRouteConstraint
{
    /// <summary>The name a route template refers to this by: <c>{**feedPath:sitefeed}</c>.</summary>
    public const string Name = "sitefeed";

    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        return values.TryGetValue(routeKey, out var value)
               && value != null
               && FeedConsts.TryParsePath(value.ToString(), out _, out _);
    }
}
