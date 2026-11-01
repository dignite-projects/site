using System;
using System.Collections.Generic;

namespace Dignite.Site.Themes;

/// <summary>
/// A whole theme as the renderer reads it: every file with its content hash and body, and the default layout
/// setting. The live site renders a release's snapshot; a preview renders the working copy's, or an older release's,
/// through the very same renderer. Declared here because both the administration side (which builds the snapshot of a
/// preview) and the public side (which reads the live one) hand it to <c>Dignite.Site.Public.Web</c>.
/// </summary>
public class SiteThemeSnapshotDto
{
    /// <summary>The release this is, or null for the working copy. What a parsed snapshot is cached by.</summary>
    public Guid? ReleaseId { get; set; }

    /// <summary>The release's number, or null for the working copy.</summary>
    public int? ReleaseNumber { get; set; }

    /// <summary>The layout of a page template whose front matter names none, or null for no layout.</summary>
    public string? DefaultLayout { get; set; }

    /// <summary>Every file, ordered by path.</summary>
    public List<SiteThemeSnapshotFileDto> Files { get; set; } = new();
}

/// <summary>One file of a <see cref="SiteThemeSnapshotDto"/>.</summary>
public class SiteThemeSnapshotFileDto
{
    public string Path { get; set; } = default!;

    /// <summary>The SHA-256 of the body: what a parsed template is cached by and what an asset's address carries.</summary>
    public string ContentHash { get; set; } = default!;

    /// <summary>
    /// The body. Empty for a static file of the live site's snapshot - a page only needs its hash, and the asset
    /// endpoint reads the body itself.
    /// </summary>
    public string Body { get; set; } = string.Empty;
}
