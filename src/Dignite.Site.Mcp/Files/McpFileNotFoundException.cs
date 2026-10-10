using Dignite.Abp.AspNetCore.Mcp.Errors;
using Dignite.Site.Mcp.Errors;
using Volo.Abp;

namespace Dignite.Site.Mcp.Files;

/// <summary>
/// A file container that is not exposed to MCP, or a file or directory that lives in one - or simply does
/// not exist. A <see cref="UserFriendlyException"/> so ABP's error converter passes the message (which names
/// the containers that do exist) through, and <see cref="IHasMcpToolErrorKind"/> because it means "not
/// found", not the business-rule "conflict" its base type would be classified as - the same reasoning as
/// <see cref="McpEntityNotFoundException"/>.
/// </summary>
public class McpFileNotFoundException : UserFriendlyException, IHasMcpToolErrorKind
{
    public McpFileNotFoundException(string message)
        : base(message, code: SiteMcpErrorCodes.FileNotFound)
    {
    }

    public string McpToolErrorKind => McpToolErrorKinds.NotFound;
}
