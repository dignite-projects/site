using System;

namespace Dignite.Site.Mcp.Files;

/// <summary>An upload link as <c>site_create_upload_url</c> hands it over: where to send the file, and how.</summary>
public class McpUploadUrl
{
    /// <summary>Send the file's bytes here as the body of a <c>PUT</c>. The link is the credential: single use, short-lived.</summary>
    public string Url { get; set; } = default!;

    public string Method { get; set; } = "PUT";

    public DateTime ExpiresAt { get; set; }

    /// <summary>The largest body the link takes, in bytes.</summary>
    public long MaxBytes { get; set; }

    public string ContainerName { get; set; } = default!;

    public string FileName { get; set; } = default!;

    /// <summary>
    /// A command that sends the file from sh, bash, zsh or cmd.exe, with <c>FILE_PATH</c> to be replaced by the path of the
    /// file. Run it on the machine the file is on; its output is the stored file, with the <c>url</c> to put in a content
    /// field. In PowerShell, where <c>curl</c> is another command, use <see cref="PowerShellCommand"/>.
    /// </summary>
    public string Command { get; set; } = default!;

    /// <summary><see cref="Command"/> for PowerShell (Windows PowerShell 5.1 and PowerShell 7).</summary>
    public string PowerShellCommand { get; set; } = default!;
}
