using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// A request's query string as <see cref="SiteRenderViewModel.Query"/> holds it: each parameter's first value, by name,
/// the name compared without regard to case as ASP.NET Core does. A visitor's text, to be read and never trusted -
/// a template writes it HTML-encoded like any value.
/// </summary>
public static class SiteRenderQuery
{
    public static IReadOnlyDictionary<string, string> Empty { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The parameters of a live request.</summary>
    public static IReadOnlyDictionary<string, string> From(IQueryCollection query)
    {
        return query.Count == 0
            ? Empty
            : query.ToDictionary(pair => pair.Key, pair => pair.Value.FirstOrDefault() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The parameters of a query string, with or without its <c>?</c> - a previewed path's.</summary>
    public static IReadOnlyDictionary<string, string> Parse(string? queryString)
    {
        return string.IsNullOrEmpty(queryString)
            ? Empty
            : QueryHelpers.ParseQuery(queryString)
                .ToDictionary(pair => pair.Key, pair => pair.Value.FirstOrDefault() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }
}
