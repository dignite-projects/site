using System;
using Volo.Abp;

namespace Dignite.Site.Mcp.Errors;

/// <summary>
/// A name the caller used does not exist - no page called <c>blogg</c>, no field called <c>titel</c>.
/// <para>
/// Its own exception type because it is the failure the name-addressed tool surface makes most likely
/// and the one a model recovers from most cheaply: the answer is always "re-read the schema", and saying
/// so in the message is worth more than any amount of generic wording. It is thrown by
/// <see cref="Naming.SiteMcpNameResolver"/> only - once a name has been resolved to an id, an entity that
/// then turns out to be missing is ABP's <c>EntityNotFoundException</c> and means something else.
/// </para>
/// </summary>
/// <remarks>
/// <b><see cref="UserFriendlyException"/>, not a plain <c>BusinessException</c>, and the difference is
/// the whole point of the type.</b> ABP's <c>IExceptionToErrorInfoConverter</c> - which
/// <see cref="McpErrorResultFactory"/> reuses - only passes an exception's own message through for
/// <c>IUserFriendlyException</c>; anything else is either localized by its error code or replaced with
/// "An internal error occurred during your request!". This code has no localization entry (its message is
/// written for a model, not a person, and is not translated), so as a plain business exception its
/// carefully worded "call get_site_schema" text would be discarded on every real request and the client
/// would be told nothing it could act on.
/// </remarks>
public class McpEntityNotFoundException : UserFriendlyException
{
    public McpEntityNotFoundException(string entityKind, string name, string? scope = null)
        : base(
            message: scope == null
                ? $"There is no {entityKind} named '{name}'. Call get_site_schema (or read the site://schema resource) for the names this site actually has."
                : $"There is no {entityKind} named '{name}' {scope}. Call get_site_schema (or read the site://schema resource) for the names this site actually has.",
            code: SiteMcpErrorCodes.NameNotFound)
    {
        EntityKind = entityKind;
        Name = name;

        WithData(nameof(entityKind), entityKind);
        WithData(nameof(name), name);
    }

    public string EntityKind { get; }

    public string Name { get; }
}

public static class SiteMcpErrorCodes
{
    /// <summary>
    /// Outside the <c>Site:0xxxxx</c> range on purpose - this is a transport-surface failure, not a
    /// domain rule, and it has no localization entry because its message is written for a model rather
    /// than for a person.
    /// </summary>
    public const string NameNotFound = "Site.Mcp:001";
}
