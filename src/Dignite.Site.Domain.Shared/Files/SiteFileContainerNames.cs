using System;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.Site.Files;

/// <summary>
/// The blob containers Site's file library stores into. A file field (GitHub issue #42) points its
/// <c>FileFieldConfiguration.FileContainerName</c> at one of these.
/// <para>
/// Containers are split by <b>policy</b> (allowed types, size limit), not by what a file is used for -
/// purpose-level grouping is what directories are for. Each container's policy is owned by
/// <c>SiteAdminApplicationModule</c>; a host only chooses the storage provider.
/// </para>
/// <para>
/// <see cref="All"/> is also the boundary of the file library: the admin API, the public read endpoint and
/// the MCP tools refuse any other container, even one the host registered for another module - a Site
/// editor's permissions say nothing about another module's blobs.
/// </para>
/// </summary>
public static class SiteFileContainerNames
{
    /// <summary>
    /// Publicly readable documents and archives for download, no pictures - a picture belongs in
    /// <see cref="Images"/>.
    /// </summary>
    public const string Attachments = "attachments";

    /// <summary>
    /// Publicly readable pictures only (raster formats, no SVG), with a tighter size limit - what an
    /// image-holding file field should point at.
    /// </summary>
    public const string Images = "images";

    /// <summary>Every container Site's file library uses.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Images, Attachments };

    /// <summary>
    /// Whether <paramref name="containerName"/> is one of <see cref="All"/>. Ordinal, the same as ABP's own
    /// container lookup: <c>Images</c> is not a container anyone configured.
    /// </summary>
    public static bool Contains(string? containerName)
    {
        return containerName != null && All.Contains(containerName, StringComparer.Ordinal);
    }
}
