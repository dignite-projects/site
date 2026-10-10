using System;
using System.Linq;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Files;

/// <summary>
/// Keeps every file tool inside the containers <see cref="SiteMcpFileOptions"/> exposes. An addition to the
/// application services' own per-container authorization, never a substitute: they still decide whether the
/// current user may act. This only decides whether MCP may ask.
/// </summary>
public class SiteMcpFileContainerGuard : ITransientDependency
{
    protected SiteMcpFileOptions Options { get; }

    public SiteMcpFileContainerGuard(IOptions<SiteMcpFileOptions> options)
    {
        Options = options.Value;
    }

    /// <summary>
    /// The exposed container named <paramref name="containerName"/>; otherwise a not-found that lists the
    /// exposed ones, which is the answer the model needs.
    /// </summary>
    public virtual SiteMcpFileContainer GetContainer(string containerName)
    {
        var container = Options.Containers.Find(containerName);
        if (container != null)
        {
            return container;
        }

        var exposed = Options.Containers.Count == 0
            ? "No file container is exposed to MCP on this server."
            : "The containers you may use are: " + string.Join(", ", Options.Containers.Select(c => $"'{c.Name}'")) + ".";

        throw new McpFileNotFoundException(
            $"There is no file container named '{containerName}' available here. {exposed} " +
            "Call site_list_file_containers for their descriptions.");
    }

    /// <summary>
    /// Refuses a file or directory, found by id, that lives outside the exposed containers - reported exactly
    /// like a missing one, so the tools do not confirm what exists elsewhere.
    /// </summary>
    public virtual void EnsureExposed(string containerName, string kind, Guid id)
    {
        if (Options.Containers.Find(containerName) == null)
        {
            throw NotFound(kind, id);
        }
    }

    /// <summary>
    /// The one answer for "nothing you may see has this id" - whether it does not exist, the caller may not
    /// read it, or it lives in a container not exposed to MCP. Distinct answers would let a caller probe ids.
    /// </summary>
    public virtual McpFileNotFoundException NotFound(string kind, Guid id)
    {
        return new McpFileNotFoundException($"There is no {kind} with id '{id}'.");
    }
}
