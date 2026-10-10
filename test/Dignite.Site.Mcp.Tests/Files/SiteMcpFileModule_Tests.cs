using System.Linq;
using Dignite.Abp.AspNetCore.Mcp;
using Dignite.Site.Files;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Site.Mcp.Files;

/// <summary>
/// The file tools as <see cref="SiteMcpModule"/> registers them: in the <c>site</c> namespace (the server
/// refuses to start otherwise), both Site containers exposed by default, and the endpoint's body limit
/// raised to fit an upload.
/// </summary>
public class SiteMcpFileModule_Tests
{
    [Fact]
    public void Should_Register_The_File_Tools_Inside_The_Site_Namespace()
    {
        using var provider = BuildProvider();

        new AbpMcpPrimitiveValidator(
                provider,
                provider.GetRequiredService<AbpMcpModuleRegistry>(),
                provider.GetRequiredService<AbpMcpPrimitiveCache>(),
                Options.Create(new AbpMcpServerOptions()))
            .Validate();

        var names = provider.GetServices<McpServerTool>().Select(tool => tool.ProtocolTool.Name).ToList();
        names.ShouldBeSubsetOf(names.Where(name => name.StartsWith("site_")));
        names.ShouldContain("site_list_file_containers");
        names.ShouldContain("site_list_directories");
        names.ShouldContain("site_create_directory");
        names.ShouldContain("site_list_files");
        names.ShouldContain("site_get_file");
        names.ShouldContain("site_upload_file");
        names.ShouldContain("site_update_file");
        names.ShouldContain("site_delete_file");
    }

    [Fact]
    public void Should_Mark_Read_Only_And_Destructive_File_Tools()
    {
        using var provider = BuildProvider();
        var tools = provider.GetServices<McpServerTool>().ToDictionary(tool => tool.ProtocolTool.Name);

        tools["site_list_files"].ProtocolTool.Annotations!.ReadOnlyHint.ShouldBe(true);
        tools["site_delete_file"].ProtocolTool.Annotations!.DestructiveHint.ShouldBe(true);
    }

    [Fact]
    public void Should_Expose_Both_Site_Containers_By_Default()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<IOptions<SiteMcpFileOptions>>().Value.Containers
            .Select(container => container.Name)
            .ShouldBe(new[] { SiteFileContainerNames.Images, SiteFileContainerNames.Default });
    }

    /// <summary>
    /// Without the registration /mcp stays at its 4 MB default and any upload past about 3 MB of file data
    /// is refused with 413.
    /// </summary>
    [Fact]
    public void Should_Raise_The_Mcp_Endpoint_Body_Limit_To_Fit_Its_Uploads()
    {
        using var provider = BuildProvider();

        var maxUploadSize = provider.GetRequiredService<IOptions<SiteMcpFileOptions>>().Value.MaxUploadSize;
        provider.GetRequiredService<IOptions<AbpMcpServerOptions>>().Value.MaxRequestBodySize
            .ShouldBe(4 * ((maxUploadSize + 2) / 3) + SiteMcpFileServerOptionsSetup.EnvelopeAllowance);
    }

    [Fact]
    public void Setup_Should_Never_Lower_A_Larger_Limit_Nor_Touch_A_Disabled_One()
    {
        var setup = new SiteMcpFileServerOptionsSetup(Options.Create(new SiteMcpFileOptions { MaxUploadSize = 1024 }));

        var larger = new AbpMcpServerOptions { MaxRequestBodySize = 50 * 1024 * 1024 };
        setup.PostConfigure(null, larger);
        larger.MaxRequestBodySize.ShouldBe(50 * 1024 * 1024);

        var disabled = new AbpMcpServerOptions { MaxRequestBodySize = null };
        setup.PostConfigure(null, disabled);
        disabled.MaxRequestBodySize.ShouldBeNull();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        var context = new ServiceConfigurationContext(services);
        var module = new SiteMcpModule();
        module.ConfigureServices(context);
        return services.BuildServiceProvider();
    }
}
