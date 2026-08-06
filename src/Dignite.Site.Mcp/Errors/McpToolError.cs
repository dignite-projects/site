using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Dignite.Site.Mcp.Errors;

/// <summary>
/// The structured payload a failed tool call returns (总体设计 §6.2.4, third footgun).
/// <para>
/// This exists so an AI client can <i>correct itself</i>. Flattening a validation failure into one
/// English sentence throws away the only part a model can act on - which field, and why - and turns a
/// one-turn fix into a guessing game. The gate already produces that detail; this carries it out intact.
/// </para>
/// </summary>
public class McpToolErrorEnvelope
{
    [JsonPropertyName("error")]
    public McpToolError Error { get; set; } = new();
}

public class McpToolError
{
    /// <summary>
    /// What kind of failure this is, so a client can decide what to do without parsing the message:
    /// <c>validation</c> (fix the fields and retry), <c>notFound</c> (re-read the schema),
    /// <c>conflict</c> (a business rule such as a duplicate slug), <c>forbidden</c> (do not retry),
    /// <c>unknown</c>.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = McpToolErrorKinds.Unknown;

    /// <summary>
    /// The domain error code where there is one, e.g. <c>Site:040001</c> for a duplicate slug. Stable
    /// across languages, unlike <see cref="Message"/>.
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    /// <summary>Per-field failures. Empty for anything other than a validation failure.</summary>
    [JsonPropertyName("validationErrors")]
    public List<McpToolValidationError> ValidationErrors { get; set; } = new();
}

public class McpToolValidationError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The field names this message is about - <c>ValidationResult.MemberNames</c>, carried through
    /// unchanged. For a content write these are field <c>Name</c>s, which are exactly the keys the
    /// caller used in its own value bag, so the client knows precisely what to change.
    /// </summary>
    [JsonPropertyName("fields")]
    public List<string> Fields { get; set; } = new();
}

public static class McpToolErrorKinds
{
    public const string Validation = "validation";
    public const string NotFound = "notFound";
    public const string Conflict = "conflict";
    public const string Forbidden = "forbidden";
    public const string Unknown = "unknown";
}
