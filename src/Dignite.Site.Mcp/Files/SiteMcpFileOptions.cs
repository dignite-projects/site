using System;
using System.Collections.ObjectModel;
using System.Linq;
using Dignite.Site.Admin.Files;
using Volo.Abp;

namespace Dignite.Site.Mcp.Files;

/// <summary>
/// Which of Site's file containers the <c>site_*</c> file tools may use, and how large an upload may travel
/// over MCP. <see cref="SiteMcpModule"/> exposes both Site containers by default; a host narrows the list with
/// <c>Configure&lt;SiteMcpFileOptions&gt;(o =&gt; ...)</c>.
/// </summary>
public class SiteMcpFileOptions
{
    /// <summary>
    /// The containers an MCP client may use, each with a description written for a model - what the
    /// container is for, so it picks the right one. A name outside <c>SiteFileContainerNames</c> is refused
    /// by the application service anyway; this list can only narrow what MCP reaches, never widen it.
    /// </summary>
    public SiteMcpFileContainerList Containers { get; } = new();

    /// <summary>
    /// The largest file, in bytes, <c>site_upload_file</c> accepts, whatever the container allows. Defaults to
    /// 5 MB. The bytes arrive base64-encoded inside a JSON-RPC message, so the request is buffered whole before
    /// any container rule runs; the MCP endpoint's request-body limit is raised, when it is lower, to just fit
    /// a file of this size (<see cref="SiteMcpFileServerOptionsSetup"/>).
    /// </summary>
    public long MaxUploadSize { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// What <c>site_upload_file</c> actually accepts for a container: the container's own limit where it has
    /// one, never more than <see cref="MaxUploadSize"/>. The one rule both the upload and the container
    /// listing use, so what is advertised is what is enforced.
    /// </summary>
    public virtual long GetMaxUploadSize(FileContainerConfigurationDto configuration)
    {
        return configuration.MaxBlobSize > 0
            ? Math.Min(configuration.MaxBlobSize, MaxUploadSize)
            : MaxUploadSize;
    }
}

public class SiteMcpFileContainer
{
    public SiteMcpFileContainer(string name, string description)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        Description = Check.NotNullOrWhiteSpace(description, nameof(description));
    }

    /// <summary>The blob container's registered name.</summary>
    public string Name { get; }

    /// <summary>What the container is for, written for a model.</summary>
    public string Description { get; }
}

/// <summary>
/// The exposed containers. A <see cref="Collection{T}"/> rather than a list, so every way in - <c>Add</c>,
/// <c>Insert</c>, the indexer - goes through the same duplicate check.
/// </summary>
public class SiteMcpFileContainerList : Collection<SiteMcpFileContainer>
{
    public SiteMcpFileContainerList Add(string name, string description)
    {
        Add(new SiteMcpFileContainer(name, description));
        return this;
    }

    /// <summary>Case-insensitive: a model writing <c>Site-Images</c> means <c>site-images</c>.</summary>
    public SiteMcpFileContainer? Find(string name)
    {
        return this.FirstOrDefault(container => string.Equals(container.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    protected override void InsertItem(int index, SiteMcpFileContainer item)
    {
        EnsureNotExposedYet(item, ignoreIndex: -1);
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, SiteMcpFileContainer item)
    {
        EnsureNotExposedYet(item, ignoreIndex: index);
        base.SetItem(index, item);
    }

    private void EnsureNotExposedYet(SiteMcpFileContainer item, int ignoreIndex)
    {
        Check.NotNull(item, nameof(item));
        for (var i = 0; i < Count; i++)
        {
            if (i != ignoreIndex && string.Equals(this[i].Name, item.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new AbpException($"The file container '{item.Name}' is already exposed to MCP.");
            }
        }
    }
}
