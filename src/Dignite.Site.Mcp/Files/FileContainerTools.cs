using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Files;

/// <summary>The entry point of the file tools: which containers exist here, and what each accepts.</summary>
[McpServerToolType]
public class FileContainerTools : ITransientDependency
{
    public ILogger<FileContainerTools> Logger { get; set; } = NullLogger<FileContainerTools>.Instance;

    protected IFileAdminAppService FileAppService { get; }

    protected SiteMcpFileOptions Options { get; }

    public FileContainerTools(IFileAdminAppService fileAppService, IOptions<SiteMcpFileOptions> options)
    {
        FileAppService = fileAppService;
        Options = options.Value;
    }

    [McpServerTool(Name = "site_list_file_containers", Title = "List file containers", ReadOnly = true)]
    [Description(
        "Lists the site's file containers you may use, with each one's purpose, the largest file you may " +
        "upload to it and its allowed file types. Call this before any other file tool: they all take a " +
        "containerName from this list.")]
    public virtual async Task<List<SiteMcpFileContainerDto>> ListFileContainersAsync()
    {
        var result = new List<SiteMcpFileContainerDto>();
        foreach (var container in Options.Containers)
        {
            FileContainerConfigurationDto configuration;
            try
            {
                configuration = await FileAppService.GetContainerConfigurationAsync(container.Name);
            }
            catch (BusinessException exception)
            {
                // A listed name the file library refuses (a typo, a container that is not Site's) is left out
                // and logged for the host to fix, rather than failing the tool every other file tool tells
                // the model to call first.
                Logger.LogWarning(exception,
                    "The file container '{ContainerName}' is exposed to MCP but could not be read, so it is left out of site_list_file_containers.",
                    container.Name);
                continue;
            }

            result.Add(new SiteMcpFileContainerDto
            {
                Name = container.Name,
                Description = container.Description,
                MaxUploadSize = Options.GetMaxUploadSize(configuration),
                AllowedFileTypes = configuration.AllowedFileTypeNames.ToList()
            });
        }

        return result;
    }
}

public class SiteMcpFileContainerDto
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>The largest file, in bytes, site_upload_file accepts for this container.</summary>
    public long MaxUploadSize { get; set; }

    /// <summary>Allowed file extensions; empty means the container does not restrict them.</summary>
    public List<string> AllowedFileTypes { get; set; } = new();
}
