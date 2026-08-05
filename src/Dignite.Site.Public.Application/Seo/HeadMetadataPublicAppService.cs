using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Routing;
using Dignite.Site.Seo;
using Schema.NET;
using SeoTags;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// The public <c>&lt;head&gt;</c>-metadata contract (总体设计 §5.3, §5.4, §5.5, §5.9 - GitHub issues #13,
/// #16, #17, #20): resolves a route, then feeds <see cref="HeadMetadataBuilder"/>'s output into
/// <c>SeoTags</c> for meta/OG/Twitter tag decisions and directly into <c>Schema.NET</c> for JSON-LD,
/// flattening both into a plain <see cref="HeadMetadataDto"/> with no third-party type in it.
/// </summary>
public class HeadMetadataPublicAppService : PublicAppService, IHeadMetadataPublicAppService
{
    protected SiteRouteResolver RouteResolver { get; }

    protected HeadMetadataBuilder HeadMetadataBuilder { get; }

    public HeadMetadataPublicAppService(SiteRouteResolver routeResolver, HeadMetadataBuilder headMetadataBuilder)
    {
        RouteResolver = routeResolver;
        HeadMetadataBuilder = headMetadataBuilder;
    }

    public virtual async Task<HeadMetadataDto?> ResolveAsync(ResolveHeadMetadataInput input)
    {
        // includeUnpublished stays false explicitly - the public surface never previews a draft, the same
        // rule RoutingPublicAppService's own resolve-path endpoint follows. The noindex-forcing that
        // obligation demands is discharged through BuildDtoAsync instead, for a future authenticated
        // preview caller that passes true there.
        var match = await RouteResolver.ResolveAsync(input.Path, input.CultureName, includeUnpublished: false);

        return match.IsMatch ? await BuildDtoAsync(match, input.CultureName, includeUnpublished: false) : null;
    }

    /// <summary>
    /// Not on the public interface. A future in-process Tier 0 renderer (#21) that already holds a
    /// resolved <see cref="RouteMatch"/> can inject this concrete class and call this directly, skipping
    /// the redundant route re-resolution <see cref="ResolveAsync"/> otherwise performs - and a future
    /// authenticated preview caller is this method's entry point for <paramref name="includeUnpublished"/>
    /// <see langword="true"/>, which <see cref="HeadMetadataBuilder"/> turns into a forced <c>noindex</c>.
    /// </summary>
    public virtual async Task<HeadMetadataDto> BuildDtoAsync(
        RouteMatch match, string cultureName, bool includeUnpublished)
    {
        var metadata = await HeadMetadataBuilder.BuildAsync(match, cultureName, includeUnpublished);
        return ConvertToDto(metadata);
    }

    protected virtual HeadMetadataDto ConvertToDto(HeadMetadata metadata)
    {
        // Collected while the JSON-LD is built, then subtracted from what the content type's schema.org
        // type expects. Presence in PropertyValues is not enough: ApplyUri rejects a relative URL and
        // ApplyString rejects a non-string, so a mapped-and-non-blank value can still be dropped - and a
        // warning that says "complete" about a node missing its image is worse than no warning.
        var appliedSchemaProperties = new HashSet<string>(StringComparer.Ordinal);
        var jsonLdDocuments = BuildJsonLdDocuments(metadata, appliedSchemaProperties);

        var missingSchemaProperties = metadata.ContentData == null
            ? new List<string>()
            : metadata.ContentData.ExpectedProperties
                .Where(name => !appliedSchemaProperties.Contains(name))
                .ToList();

        // "Feed it, don't reimplement it" (GitHub issue #13): SeoInfo decides the Twitter card type from
        // what is fed in (an image means summary_large_image, no image means summary), rather than this
        // service hand-picking either.
        var seoInfo = new SeoInfo();
        // SeoTags rejects a blank description outright (SetCommonInfo -> EnsureNotNullOrWhiteSpace), but
        // not every content has one - falling back to the title keeps this call from throwing without
        // misrepresenting the DTO, since MetaDescription below is set from metadata.Description directly
        // and this fed-in value is never read back out of seoInfo.
        seoInfo.SetCommonInfo(
            metadata.Title, metadata.Description ?? metadata.Title, metadata.CanonicalUrl, Array.Empty<string>());
        seoInfo.MetaLink.Robots = metadata.NoIndex ? "noindex" : null;

        if (metadata.OgImageUrl != null)
        {
            seoInfo.SetImageInfo(metadata.OgImageUrl);
        }

        // Unlike the Twitter card type, SeoTags never sees enough to pick the OG type itself - nothing
        // here calls SetArticleInfo/SetProducInfo, so OpenGraph.Type would otherwise sit at its Website
        // default even for a page whose JSON-LD (built below) already knows it is an Article or Product.
        seoInfo.OpenGraph.Type = metadata.ContentData?.SchemaType switch
        {
            SchemaOrgType.Article or SchemaOrgType.NewsArticle => OpenGraphType.Article,
            SchemaOrgType.Product => OpenGraphType.Product,
            _ => OpenGraphType.Website
        };

        return new HeadMetadataDto
        {
            MetaTitle = metadata.Title,
            MetaDescription = metadata.Description,
            CanonicalUrl = metadata.CanonicalUrl,
            RobotsContent = seoInfo.MetaLink.Robots,
            OgImageUrl = seoInfo.OpenGraph.ImageUrl,
            OgType = ToWireValue(seoInfo.OpenGraph.Type),
            TwitterCardType = ToWireValue(seoInfo.TwitterCard.CardType),
            HreflangAlternates = metadata.HreflangAlternates
                .Select(a => new HreflangAlternateDto { CultureName = a.CultureName, Url = a.Url })
                .ToList(),
            XDefaultUrl = metadata.XDefaultUrl,
            MissingSchemaProperties = missingSchemaProperties,
            JsonLdDocuments = jsonLdDocuments
        };
    }

    /// <summary>
    /// The actual <c>og:type</c>/<c>twitter:card</c> attribute values SeoTags would render (e.g.
    /// <c>"article"</c>, <c>"summary_large_image"</c>). SeoTags keeps this mapping
    /// (<c>Utilities.ToDisplay&lt;TEnum&gt;</c>) <c>internal</c>, so nothing outside the library can reach
    /// it - confirmed by reflection - and the flattened <see cref="HeadMetadataDto"/> carries plain
    /// strings a renderer uses as-is, not the SeoTags enum type, so it has to be re-derived here.
    /// </summary>
    private static string? ToWireValue(OpenGraphType? type)
    {
        return type switch
        {
            null => null,
            OpenGraphType.Website => "website",
            OpenGraphType.Article => "article",
            OpenGraphType.Product => "product",
            OpenGraphType.Book => "book",
            _ => type.Value.ToString().ToLowerInvariant()
        };
    }

    private static string? ToWireValue(TwitterCardType? type)
    {
        return type switch
        {
            null => null,
            TwitterCardType.Summary => "summary",
            TwitterCardType.SummaryLargeImage => "summary_large_image",
            TwitterCardType.Player => "player",
            _ => type.Value.ToString().ToLowerInvariant()
        };
    }

    /// <summary>
    /// Built directly against <c>Schema.NET</c>'s own types rather than <c>SeoTags.JsonLd</c>'s simplified
    /// <c>*Info</c> wrappers: those wrappers drop properties this needs (<c>ArticleInfo</c> has no
    /// <c>articleBody</c>, for one) and their <c>ConvertTo()</c> was found to throw on an unset optional
    /// collection during manual verification - not something to depend on for tenant-driven content.
    /// </summary>
    /// <param name="appliedSchemaProperties">
    /// Receives every mapped schema.org property this actually set on the emitted node - the basis for
    /// <see cref="HeadMetadataDto.MissingSchemaProperties"/>.
    /// </param>
    protected virtual List<string> BuildJsonLdDocuments(
        HeadMetadata metadata, ISet<string> appliedSchemaProperties)
    {
        var documents = new List<string>
        {
            BuildOrganization(metadata.SiteData).ToString(),
            BuildWebSite(metadata.SiteData, metadata.CultureName).ToString()
        };

        if (metadata.Breadcrumb.Count > 1)
        {
            // A one-entry breadcrumb is the home page pointing at itself: Schema.NET collapses a
            // single-element ItemListElement to a bare object rather than an array, and Google needs at
            // least two levels to render a trail, so the node would be malformed and useless at once.
            documents.Add(BuildBreadcrumbList(metadata.Breadcrumb).ToString());
        }

        if (metadata.ContentData != null)
        {
            var contentNode = BuildContentNode(
                metadata.CanonicalUrl, metadata.CultureName, metadata.ContentData, appliedSchemaProperties);

            if (contentNode != null)
            {
                documents.Add(contentNode);
            }
        }

        return documents.Select(EscapeForScriptEmbedding).ToList();
    }

    /// <summary>
    /// Neutralizes <c>&lt;</c> so tenant-authored text landing in a JSON-LD string value (an article body,
    /// a breadcrumb name, ...) cannot break out of the <c>&lt;script type="application/ld+json"&gt;</c>
    /// block a renderer embeds this string in verbatim with a literal <c>&lt;/script&gt;</c>. Schema.NET's
    /// own serialization does not escape this - confirmed by reproducing the injection - and <c><</c>
    /// is a semantically identical JSON string character, so this changes nothing about the parsed value.
    /// </summary>
    private static string EscapeForScriptEmbedding(string json)
    {
        return json.Replace("<", "\\u003c");
    }

    protected virtual Organization BuildOrganization(JsonLdSiteData siteData)
    {
        var organization = new Organization
        {
            Name = siteData.OrganizationName,
            Url = new Uri(siteData.BaseUrl)
        };

        if (siteData.OrganizationLogoUrl != null && Uri.TryCreate(siteData.OrganizationLogoUrl, UriKind.Absolute, out var logoUri))
        {
            organization.Logo = logoUri;
        }

        return organization;
    }

    protected virtual WebSite BuildWebSite(JsonLdSiteData siteData, string cultureName)
    {
        return new WebSite
        {
            Name = siteData.OrganizationName,
            Url = new Uri(siteData.BaseUrl),
            InLanguage = cultureName
        };
    }

    protected virtual BreadcrumbList BuildBreadcrumbList(IReadOnlyList<JsonLdBreadcrumbItem> breadcrumb)
    {
        // List<IListItem>, not an array or List<ListItem> - Schema.NET's Values<> implicit conversion
        // from a covariant array threw at runtime during manual verification; the interface-typed list is
        // the form that actually works.
        var items = new List<IListItem>();

        for (var i = 0; i < breadcrumb.Count; i++)
        {
            items.Add(new ListItem
            {
                Position = i + 1,
                Name = breadcrumb[i].Name,
                Item = new Thing { Id = new Uri(breadcrumb[i].Url) }
            });
        }

        return new BreadcrumbList { ItemListElement = items };
    }

    /// <summary>
    /// The one place the tenant-chosen <c>ContentTypeField.SchemaProperty</c> vocabulary (总体设计 §5.4,
    /// GitHub issue #20) is interpreted - Domain deliberately does not know these names, only that they
    /// exist. Null when <paramref name="contentData"/>'s schema type has no node builder here (there is
    /// none beyond the three the design doc names as still producing rich results).
    /// </summary>
    protected virtual string? BuildContentNode(
        string canonicalUrl, string cultureName, JsonLdContentData contentData, ISet<string> applied)
    {
        var values = contentData.PropertyValues;
        var canonicalUri = new Uri(canonicalUrl);

        switch (contentData.SchemaType)
        {
            case SchemaOrgType.Article:
            case SchemaOrgType.NewsArticle:
                var article = contentData.SchemaType == SchemaOrgType.NewsArticle ? new NewsArticle() : new Article();
                ApplyString(v => article.Headline = v, values, "headline", applied);
                ApplyString(v => article.Description = v, values, "description", applied);
                ApplyUri(v => article.Image = v, values, "image", applied);
                ApplyString(v => article.ArticleBody = v, values, "articleBody", applied);
                ApplyString(v => article.Author = new Person { Name = v }, values, "authorName", applied);
                article.Url = canonicalUri;
                article.MainEntityOfPage = canonicalUri;
                article.InLanguage = cultureName;
                // Clock.ToOffset, not a hard-coded +00:00 - ABP's IClock defaults to DateTimeKind.Unspecified,
                // so a literal TimeSpan.Zero would mislabel a local server time as UTC and shift every date
                // by the server's offset (see SeoClock's own remarks; every other SEO date path uses it).
                article.DatePublished = Clock.ToOffset(contentData.PublishTime);
                article.DateModified = Clock.ToOffset(contentData.ModifiedTime);
                return article.ToString();

            case SchemaOrgType.Product:
                var product = new Product { Url = canonicalUri };
                ApplyString(v => product.Name = v, values, "name", applied);
                ApplyString(v => product.Description = v, values, "description", applied);
                ApplyUri(v => product.Image = v, values, "image", applied);
                ApplyString(v => product.Sku = v, values, "sku", applied);
                ApplyString(v => product.Brand = new Brand { Name = v }, values, "brand", applied);

                var offer = BuildOffer(values);
                if (offer != null)
                {
                    product.Offers = offer;
                    applied.Add("price");
                }

                return product.ToString();

            default:
                return null;
        }
    }

    /// <summary>
    /// Null when no price is mapped, or the mapped value cannot be read as a number - an Offer with no
    /// price is not a meaningful node to emit, and this must fail open the same way every sibling reader
    /// in this file does (<see cref="ApplyString"/>, <see cref="ApplyUri"/>): a tenant mapping a text field
    /// to <c>"price"</c> and typing <c>"Contact us"</c> must not 500 the entire head-metadata response.
    /// </summary>
    protected virtual Offer? BuildOffer(IReadOnlyDictionary<string, object?> values)
    {
        if (!values.TryGetValue("price", out var priceValue) || priceValue == null
            || !TryToDecimal(priceValue, out var price))
        {
            return null;
        }

        var offer = new Offer { Price = price };

        if (values.TryGetValue("priceCurrency", out var currency) && currency is string currencyText)
        {
            offer.PriceCurrency = currencyText;
        }

        if (values.TryGetValue("availability", out var availability)
            && availability is string availabilityText
            && TryParseAvailability(availabilityText, out var parsed))
        {
            offer.Availability = parsed;
        }

        return offer;
    }

    /// <summary>
    /// Accepts whatever numeric shape the flex-field bag happens to hold - a live <c>decimal</c>/
    /// <c>double</c> from a Number field, or a plain string from a Text field a tenant repurposed - and
    /// fails closed rather than throwing, including on a value too large for <see cref="decimal"/> (an
    /// overflowing <c>double</c> reaches here intact, since <c>JsonElement.GetDouble</c> yields
    /// <see cref="double.PositiveInfinity"/> rather than throwing).
    /// <para>
    /// String parsing is always <see cref="CultureInfo.InvariantCulture"/> and deliberately narrow:
    /// <see cref="Convert.ToDecimal(object)"/> would use the request's <c>CurrentCulture</c>, so the same
    /// stored value would publish a different price per visitor language; and <c>NumberStyles.Number</c>
    /// would accept thousands separators, which .NET does not group-check - <c>"1,2,3"</c> parses to
    /// <c>123</c>. A price is also never negative, so a leading sign is refused rather than published.
    /// </para>
    /// </summary>
    private const NumberStyles PriceNumberStyles =
        NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;

    private static bool TryToDecimal(object value, out decimal result)
    {
        result = default;

        switch (value)
        {
            case decimal d:
                result = d;
                return result >= 0;
            case double or float or byte or sbyte or short or ushort or int or uint or long or ulong:
                try
                {
                    result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                }
                catch (OverflowException)
                {
                    return false;
                }

                return result >= 0;
            case string s:
                return decimal.TryParse(s, PriceNumberStyles, CultureInfo.InvariantCulture, out result)
                       && result >= 0;
            default:
                return false;
        }
    }

    /// <summary>
    /// Accepts the canonical schema.org URL form (<c>https://schema.org/InStock</c> - what
    /// <c>Schema.NET</c> itself serializes, and what a tenant is most likely to copy from prior output or
    /// from schema.org's own docs), and the bare enum name in any casing or spacing (<c>"in stock"</c>,
    /// <c>"IN_STOCK"</c>).
    /// <para>
    /// Refuses two shapes <see cref="Enum.TryParse"/> would otherwise wave through. A purely numeric string
    /// parses to whichever member holds that value, so a numeric field mapped here by mistake would emit a
    /// confidently wrong availability. And a comma is <see cref="Enum.TryParse"/>'s flag-list syntax, so
    /// <c>"InStock, SoldOut"</c> would OR into an undefined member and serialize as a bare integer where
    /// schema.org requires a URL - which invalidates the whole Offer node. <see cref="Enum.IsDefined"/> is
    /// the backstop for anything else that produces an out-of-range value.
    /// </para>
    /// </summary>
    private static bool TryParseAvailability(string text, out ItemAvailability availability)
    {
        availability = default;

        if (text.Contains(','))
        {
            return false;
        }

        // TrimEnd first: a copy-pasted "https://schema.org/InStock/" would otherwise yield an empty
        // last segment and be dropped silently.
        var name = text.Trim().TrimEnd('/');
        var lastSlash = name.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            name = name[(lastSlash + 1)..];
        }

        name = name.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);

        if (name.Length == 0 || name.Any(char.IsDigit))
        {
            return false;
        }

        return Enum.TryParse(name, ignoreCase: true, out availability)
               && Enum.IsDefined(availability);
    }

    private static void ApplyString(
        Action<string> setter, IReadOnlyDictionary<string, object?> values, string key, ISet<string> applied)
    {
        if (values.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text))
        {
            setter(text);
            applied.Add(key);
        }
    }

    private static void ApplyUri(
        Action<Uri> setter, IReadOnlyDictionary<string, object?> values, string key, ISet<string> applied)
    {
        if (values.TryGetValue(key, out var value) && value is string text && Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            setter(uri);
            applied.Add(key);
        }
    }
}
