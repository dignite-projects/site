using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dignite.FileExplorer.Files;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Content;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Files;

/// <summary>
/// GitHub issue #41: proves the backend Site stood up for Dignite.FileExplorer actually works end to
/// end - a real file through <see cref="FileDescriptorManager"/>, against the filesystem container the
/// test module configures under <see cref="SiteFileContainerNames.Default"/>, tenant-scoped by the same
/// <see cref="ICurrentTenant"/> every other Site entity uses (no separate tenant-mapping code exists,
/// because none is needed - FileDescriptor is plain ABP IMultiTenant).
/// </summary>
public class FileExplorerBackend_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly ICurrentTenant _currentTenant;

    public FileExplorerBackend_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Should_Store_And_Read_Back_A_Real_File_Tenant_Scoped()
    {
        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello from issue #41"));
            var content = new RemoteStreamContent(stream, "hello.txt", "text/plain");

            return await _fileDescriptorManager.CreateAsync(
                SiteFileContainerNames.Default, content, cellName: null, directoryId: null, entityId: null);
        });

        descriptor.TenantId.ShouldBe(_currentTenant.Id);
        descriptor.Name.ShouldBe("hello.txt");

        var readBack = await WithUnitOfWorkAsync(() => _fileDescriptorManager.GetStreamOrNullAsync(
            SiteFileContainerNames.Default, descriptor.BlobName));

        readBack.ShouldNotBeNull();
        using var reader = new StreamReader(readBack!);
        (await reader.ReadToEndAsync()).ShouldBe("hello from issue #41");
    }
}
