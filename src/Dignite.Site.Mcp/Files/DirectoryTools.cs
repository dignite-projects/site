using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Site.Admin.Directories;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Files;

/// <summary>
/// Reading the caller's directory tree and adding to it. Moving, renaming and deleting directories are left
/// to the admin UI: a directory carries files with it, a larger change than an assistant should make in
/// passing.
/// </summary>
[McpServerToolType]
public class DirectoryTools : ITransientDependency
{
    protected IDirectoryAdminAppService DirectoryAppService { get; }

    protected SiteMcpFileContainerGuard ContainerGuard { get; }

    public DirectoryTools(IDirectoryAdminAppService directoryAppService, SiteMcpFileContainerGuard containerGuard)
    {
        DirectoryAppService = directoryAppService;
        ContainerGuard = containerGuard;
    }

    [McpServerTool(Name = "site_list_directories", Title = "List directories", ReadOnly = true)]
    [Description("Returns your directory tree in a file container. Each directory carries its id and its children.")]
    public virtual async Task<IReadOnlyList<DirectoryDescriptorInfoDto>> ListDirectoriesAsync(
        [Description("A container name from site_list_file_containers.")]
        string containerName)
    {
        var container = ContainerGuard.GetContainer(containerName);
        var result = await DirectoryAppService.GetListAsync(new GetDirectoriesInput { ContainerName = container.Name });
        return result.Items;
    }

    [McpServerTool(Name = "site_create_directory", Title = "Create a directory")]
    [Description("Creates a directory in a file container, at the top level or under one of your directories.")]
    public virtual async Task<DirectoryDescriptorDto> CreateDirectoryAsync(
        [Description("A container name from site_list_file_containers.")]
        string containerName,
        [Description("The new directory's name.")]
        string name,
        [Description("The id of the directory to create it under, from site_list_directories. Omit for the top level.")]
        Guid? parentId = null)
    {
        var container = ContainerGuard.GetContainer(containerName);
        return await DirectoryAppService.CreateAsync(new CreateDirectoryInput
        {
            ContainerName = container.Name,
            Name = name,
            ParentId = parentId
        });
    }
}
