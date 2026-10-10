using System.Threading.Tasks;
using Dignite.Site.Admin.Files;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Mcp.Files;

public class FileContainerTools_Tests
{
    [Fact]
    public async Task Should_List_Only_Exposed_Containers_With_Their_Effective_Upload_Limit()
    {
        var fileAppService = Substitute.For<IFileAdminAppService>();
        fileAppService.GetContainerConfigurationAsync("site-images").Returns(new FileContainerConfigurationDto
        {
            MaxBlobSize = 1024,
            AllowedFileTypeNames = new[] { ".png", ".jpg" }
        });
        fileAppService.GetContainerConfigurationAsync("site-files").Returns(new FileContainerConfigurationDto
        {
            MaxBlobSize = 100 * 1024 * 1024
        });

        var options = new SiteMcpFileOptions { MaxUploadSize = 2048 };
        options.Containers
            .Add("site-images", "Images used in site content.")
            .Add("site-files", "Downloadable attachments.");

        var containers = await new FileContainerTools(fileAppService, Options.Create(options)).ListFileContainersAsync();

        containers.Count.ShouldBe(2);
        containers[0].Name.ShouldBe("site-images");
        containers[0].Description.ShouldBe("Images used in site content.");
        containers[0].MaxUploadSize.ShouldBe(1024);
        containers[0].AllowedFileTypes.ShouldBe(new[] { ".png", ".jpg" });
        // The container allows 100 MB, but nothing larger than the module limit travels as base64.
        containers[1].MaxUploadSize.ShouldBe(2048);
    }

    /// <summary>One bad name in the host's list must not take the entry-point tool down for the containers that work.</summary>
    [Fact]
    public async Task Should_Leave_Out_A_Container_That_Cannot_Be_Read()
    {
        var fileAppService = Substitute.For<IFileAdminAppService>();
        fileAppService.GetContainerConfigurationAsync("site-image")
            .Returns<Task<FileContainerConfigurationDto>>(_ => throw new BusinessException("Site:080001"));
        fileAppService.GetContainerConfigurationAsync("site-files").Returns(new FileContainerConfigurationDto());

        var options = new SiteMcpFileOptions();
        options.Containers
            .Add("site-image", "A typo for site-images.")
            .Add("site-files", "Downloadable attachments.");

        var containers = await new FileContainerTools(fileAppService, Options.Create(options)).ListFileContainersAsync();

        containers.ShouldHaveSingleItem().Name.ShouldBe("site-files");
    }

    [Fact]
    public void Should_Refuse_A_Duplicate_Container_However_It_Is_Added()
    {
        var containers = new SiteMcpFileOptions().Containers.Add("site-images", "Images.");

        Should.Throw<AbpException>(() => containers.Add("SITE-IMAGES", "Again."));
        Should.Throw<AbpException>(() => containers.Add(new SiteMcpFileContainer("site-images", "Again.")));
        Should.Throw<AbpException>(() => containers.Insert(0, new SiteMcpFileContainer("site-images", "Again.")));
        containers.Count.ShouldBe(1);
    }
}
