using System;
using System.Collections.Generic;
using Dignite.Site.Contents;

namespace Dignite.Site.Mcp.Contents;

/// <summary>
/// Where a content is, without what is in it: what <c>site_create_content</c>, <c>site_update_content</c> and
/// <c>site_list_contents</c> return, instead of the content as the application service has it.
/// <para>
/// That is audit columns, two Guids no tool takes, an address that is empty on the administration side, and above all
/// the field values - a page's whole body, sent back to the client that has just sent it, and in a list sent for every
/// row. The client addresses a content by page, language and slug and has the values already; what it needs back is
/// confirmation, and <c>site_get_content</c> when it wants the values.
/// </para>
/// </summary>
public class McpContentSummary
{
    public string Page { get; set; } = default!;

    public string ContentType { get; set; } = default!;

    public string CultureName { get; set; } = default!;

    public string Slug { get; set; } = default!;

    public ContentStatus Status { get; set; }

    public DateTime PublishTime { get; set; }

    /// <summary>
    /// After a create or an update: the names of the values that were stored. A value the client sent that is missing here was
    /// dropped, because the content type does not declare the field.
    /// </summary>
    public string[]? StoredFields { get; set; }

    /// <summary>In a list, when it was asked for: the values, keyed by field name. Left out otherwise.</summary>
    public IDictionary<string, object?>? FieldValues { get; set; }
}
