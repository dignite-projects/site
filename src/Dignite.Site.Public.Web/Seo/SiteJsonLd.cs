using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using Dignite.Site.Files;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// Writes the structured data a template gives - one schema.org node or several - as the content of a
/// <c>&lt;script type="application/ld+json"&gt;</c>. Site does not choose the shape (总体设计 §5.4, GitHub issue #40):
/// which types a page carries and what goes in them is the template's. What it takes off every template's hands is
/// writing them correctly:
/// <list type="bullet">
/// <item>Several nodes become one <c>@graph</c> under one <c>@context</c>; a single node gets the <c>@context</c> itself.</item>
/// <item>A property with nothing in it - null, a blank string, an empty list, an object left empty - is left out, at any
/// depth, so a template writes <c>author: content.fields.author</c> without first asking whether there is one.</item>
/// <item>An address written <c>~/...</c>, and a file of the site's library, is made absolute on the site's primary domain,
/// as structured data needs (see <see cref="CreateUrlResolver"/>).</item>
/// <item><c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c> are escaped (<c><</c>...), so a title holding <c>&lt;/script&gt;</c>
/// cannot end the script early - the stored XSS GitHub issue #20 found.</item>
/// </list>
/// Each node must be an object with an <c>@type</c>; anything else is refused with an <see cref="ArgumentException"/>,
/// which a Scriban template reports at its line and the theme check's trial render finds before a publish.
/// </summary>
public static class SiteJsonLd
{
    public const string SchemaOrgContext = "https://schema.org";

    // Every Unicode range is let through, so Chinese and Japanese text is written as it is rather than as \uXXXX;
    // the encoder still escapes the characters that matter inside HTML (<, >, &, ' and ").
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false
    };

    /// <summary>
    /// The JSON text of <paramref name="schema"/>, a node (a JSON object) or a list of them; null for an empty list.
    /// </summary>
    /// <param name="schema">The node or nodes.</param>
    /// <param name="resolveUrl">Rewrites each string value - see <see cref="CreateUrlResolver"/>; null leaves them as they are.</param>
    /// <exception cref="ArgumentException">A node is not an object, or has no <c>@type</c>.</exception>
    public static string? Serialize(JsonNode? schema, Func<string, string>? resolveUrl = null)
    {
        var nodes = schema switch
        {
            JsonObject node => new List<JsonNode?> { node },
            JsonArray list => list.ToList(),
            null => throw new ArgumentException("The structured data is empty: give a node, such as { \"@type\": \"Organization\", name: \"...\" }, or a list of nodes."),
            _ => throw new ArgumentException("The structured data must be a node, such as { \"@type\": \"Organization\", name: \"...\" }, or a list of nodes.")
        };

        var cleaned = new List<JsonObject>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is not JsonObject node)
            {
                throw new ArgumentException($"Structured data node {i + 1} is not an object: each node is an object with an \"@type\".");
            }

            if (!HasType(node))
            {
                throw new ArgumentException($"Structured data node {i + 1} has no \"@type\", such as \"Organization\" or \"BreadcrumbList\".");
            }

            cleaned.Add((JsonObject)Clean(node, resolveUrl ?? (value => value))!);
        }

        if (cleaned.Count == 0)
        {
            return null;
        }

        JsonObject root;
        if (cleaned.Count == 1)
        {
            root = cleaned[0];
            if (!root.ContainsKey("@context"))
            {
                // @context first, where readers and validators look for it.
                var withContext = new JsonObject { ["@context"] = SchemaOrgContext };
                foreach (var (name, value) in root.ToList())
                {
                    root.Remove(name);
                    withContext[name] = value;
                }

                root = withContext;
            }
        }
        else
        {
            var graph = new JsonArray();
            foreach (var node in cleaned)
            {
                graph.Add(node);
            }

            root = new JsonObject { ["@context"] = SchemaOrgContext, ["@graph"] = graph };
        }

        return root.ToJsonString(WriteOptions);
    }

    /// <summary>
    /// The rewrite <see cref="Serialize"/> applies to each string: <c>~/...</c> is put in the page's language by
    /// <paramref name="localize"/> (the rule <c>url</c> and a Razor <c>~/</c> link follow) and then on
    /// <paramref name="baseUrl"/>; an address of the site's file library (<c>/api/site-public/files/...</c>) is put on
    /// <paramref name="baseUrl"/>. Any other string is left as it is. Without a <paramref name="baseUrl"/>, the
    /// addresses stay site-relative.
    /// </summary>
    /// <param name="baseUrl">The site's primary origin, <see cref="HeadMetadataDto.BaseUrl"/>.</param>
    /// <param name="localize">Puts the language prefix on a <c>~/</c> address and returns it, still starting with <c>~/</c>.</param>
    public static Func<string, string> CreateUrlResolver(string? baseUrl, Func<string, string> localize)
    {
        var root = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl.TrimEnd('/');
        var filePrefix = "/" + SiteFileUrl.RoutePrefix + "/";

        return value =>
        {
            if (value.StartsWith("~/", StringComparison.Ordinal))
            {
                var localized = localize(value);
                return root + (localized.StartsWith('~') ? localized[1..] : localized);
            }

            return value.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase) ? root + value : value;
        };
    }

    private static bool HasType(JsonObject node)
    {
        return node["@type"] switch
        {
            JsonValue value => value.TryGetValue<string>(out var type) && !string.IsNullOrWhiteSpace(type),
            JsonArray types => types.Count > 0,
            _ => false
        };
    }

    /// <summary>A copy of <paramref name="node"/> without its empty parts and with its strings rewritten; null when nothing is left.</summary>
    private static JsonNode? Clean(JsonNode? node, Func<string, string> resolveUrl)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject obj:
            {
                var result = new JsonObject();
                foreach (var (name, value) in obj)
                {
                    var cleaned = Clean(value, resolveUrl);
                    if (cleaned != null)
                    {
                        result[name] = cleaned;
                    }
                }

                return result.Count == 0 ? null : result;
            }
            case JsonArray array:
            {
                var result = new JsonArray();
                foreach (var item in array)
                {
                    var cleaned = Clean(item, resolveUrl);
                    if (cleaned != null)
                    {
                        result.Add(cleaned);
                    }
                }

                return result.Count == 0 ? null : result;
            }
            // A date is a string in JSON too, but not a string value to rewrite: TryGetValue<string> tells the two apart.
            case JsonValue value when value.TryGetValue<string>(out var text):
                return string.IsNullOrWhiteSpace(text) ? null : JsonValue.Create(resolveUrl(text));
            case JsonValue value when value.GetValueKind() == JsonValueKind.Null:
                return null;
            default:
                return node.DeepClone();
        }
    }
}
