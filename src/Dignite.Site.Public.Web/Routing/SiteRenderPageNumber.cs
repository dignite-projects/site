using System.Globalization;
using Dignite.Site.Public.Scriban.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// Reads the page number of a paged list off a request - <see cref="SiteRenderViewModel.CurrentPage"/>. The value
/// is a visitor's, so nothing in it fails the render: what is not a whole number from 1 up is page 1, and a number
/// beyond <see cref="ContentsFunction.MaxPageNumber"/> - past which a list query's skip count would overflow - is
/// that page. The same rule a route filter value follows: untrusted input is read leniently, never refused.
/// </summary>
public static class SiteRenderPageNumber
{
    /// <summary>The page number <paramref name="value"/> - the page query parameter's value - says; 1 when it says none.</summary>
    public static int Parse(StringValues value)
    {
        // Several values (?page=2&page=3): the first one, as a query string reader would bind it.
        var text = value.Count > 0 ? value[0] : null;

        if (string.IsNullOrWhiteSpace(text)
            || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || number < 1)
        {
            return 1;
        }

        return number > ContentsFunction.MaxPageNumber ? ContentsFunction.MaxPageNumber : number;
    }

    /// <summary>The page number <paramref name="query"/> carries under <paramref name="pageQueryKey"/>; 1 when it carries none.</summary>
    public static int Parse(IQueryCollection query, string pageQueryKey)
    {
        return Parse(query[pageQueryKey]);
    }

    /// <summary>
    /// The page number a query string (<c>?page=2</c>, with or without the leading <c>?</c>, or empty) carries under
    /// <paramref name="pageQueryKey"/>; 1 when it carries none.
    /// </summary>
    public static int Parse(string? queryString, string pageQueryKey)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            return 1;
        }

        var query = QueryHelpers.ParseQuery(queryString);
        return query.TryGetValue(pageQueryKey, out var value) ? Parse(value) : 1;
    }
}
