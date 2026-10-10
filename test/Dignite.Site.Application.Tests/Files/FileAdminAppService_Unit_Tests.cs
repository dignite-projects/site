using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Admin.Directories;
using Dignite.Site.Directories;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;
using Xunit;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// The parts of the file library's application services that depend on who the caller is - which the
/// suite's always-allow authorization would otherwise hide.
/// </summary>
public class FileAdminAppService_Unit_Tests
{
    private const string Container = SiteFileContainerNames.Images;

    [Fact]
    public async Task GetList_Should_Return_Nothing_For_A_Caller_Without_A_User()
    {
        var fileRepository = Substitute.For<IFileDescriptorRepository>();
        var appService = CreateFileAppService(fileRepository, Substitute.For<IDirectoryDescriptorRepository>(), userId: null, allowEverything: false);

        var result = await appService.GetListAsync(new GetFilesInput { ContainerName = Container });

        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
        await fileRepository.DidNotReceiveWithAnyArgs().GetCountAsync(default!, default, default, default, default);
    }

    /// <summary>Without SiteAdmin.Contents a caller sees their own files only, whatever creator they asked for.</summary>
    [Fact]
    public async Task GetList_Should_Filter_To_The_Callers_Own_Files_Without_Contents_Management()
    {
        var userId = Guid.NewGuid();
        var fileRepository = Substitute.For<IFileDescriptorRepository>();
        fileRepository.GetListAsync(default!).ReturnsForAnyArgs(new List<FileDescriptor>());
        var appService = CreateFileAppService(fileRepository, Substitute.For<IDirectoryDescriptorRepository>(), userId, allowEverything: false);

        await appService.GetListAsync(new GetFilesInput { ContainerName = Container, CreatorId = Guid.NewGuid() });

        await fileRepository.Received().GetCountAsync(Container, userId, Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetList_Should_Refuse_A_Container_That_Is_Not_Sites()
    {
        var appService = CreateFileAppService(Substitute.For<IFileDescriptorRepository>(), Substitute.For<IDirectoryDescriptorRepository>(), Guid.NewGuid(), allowEverything: true);

        var exception = await Should.ThrowAsync<FileContainerNotAvailableException>(() =>
            appService.GetListAsync(new GetFilesInput { ContainerName = "invoices" }));

        exception.Code.ShouldBe(SiteErrorCodes.FileContainerNotAvailable);
    }

    /// <summary>A partial update must tell "not sent" from an explicit null (move to the root).</summary>
    [Fact]
    public void UpdateInput_Should_Track_An_Explicit_Null_Directory()
    {
        var sent = JsonSerializer.Deserialize<UpdateFileInput>("{\"directoryId\":null}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var omitted = JsonSerializer.Deserialize<UpdateFileInput>("{\"name\":\"a.png\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        sent.DirectoryIdSpecified.ShouldBeTrue();
        omitted.DirectoryIdSpecified.ShouldBeFalse();
    }

    [Fact]
    public async Task Rename_Should_Keep_The_Directory()
    {
        var ownerId = Guid.NewGuid();
        var directoryId = Guid.NewGuid();
        var file = new FileDescriptor(
            Guid.NewGuid(), Container, "blob-name", "old-name.png", "image/png", 1,
            hash: null, referBlobName: null, directoryId, tenantId: null)
        {
            CreatorId = ownerId
        };
        var fileRepository = Substitute.For<IFileDescriptorRepository>();
        fileRepository.GetAsync(file.Id).Returns(file);
        var directoryRepository = Substitute.For<IDirectoryDescriptorRepository>();

        var appService = CreateFileAppService(fileRepository, directoryRepository, ownerId, allowEverything: true);

        await appService.UpdateAsync(file.Id, new UpdateFileInput { Name = "new-name.png" });

        file.Name.ShouldBe("new-name.png");
        file.DirectoryId.ShouldBe(directoryId);
        await directoryRepository.DidNotReceiveWithAnyArgs().FindAsync(default, default, default);
    }

    [Fact]
    public async Task Directories_Should_Refuse_A_Caller_Without_A_User()
    {
        var repository = Substitute.For<IDirectoryDescriptorRepository>();
        var appService = CreateDirectoryAppService(repository);

        var list = await Should.ThrowAsync<AbpAuthorizationException>(() =>
            appService.GetListAsync(new GetDirectoriesInput { ContainerName = Container }));
        var create = await Should.ThrowAsync<AbpAuthorizationException>(() =>
            appService.CreateAsync(new CreateDirectoryInput { ContainerName = Container, Name = "images" }));

        list.Code.ShouldBe(SiteErrorCodes.DirectoryRequiresUser);
        create.Code.ShouldBe(SiteErrorCodes.DirectoryRequiresUser);
        await repository.DidNotReceiveWithAnyArgs().GetAllByUserAsync(default, default!, default);
    }

    private static FileAdminAppService CreateFileAppService(
        IFileDescriptorRepository fileRepository,
        IDirectoryDescriptorRepository directoryRepository,
        Guid? userId,
        bool allowEverything)
    {
        var authorizationService = Substitute.For<IAbpAuthorizationService>();
        var result = allowEverything ? AuthorizationResult.Success() : AuthorizationResult.Failed();
        authorizationService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>()).Returns(result);
        authorizationService.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<IEnumerable<IAuthorizationRequirement>>()).Returns(result);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns(userId);

        var objectMapper = Substitute.For<IObjectMapper<SiteAdminApplicationModule>>();
        objectMapper.Map<List<FileDescriptor>, List<FileDescriptorDto>>(Arg.Any<List<FileDescriptor>>()).Returns(new List<FileDescriptorDto>());
        objectMapper.Map<FileDescriptor, FileDescriptorDto>(Arg.Any<FileDescriptor>()).Returns(new FileDescriptorDto());

        var services = new ServiceCollection()
            .AddSingleton(authorizationService)
            .AddSingleton<IAuthorizationService>(authorizationService)
            .AddSingleton(currentUser)
            .AddSingleton(objectMapper)
            .AddSingleton<IObjectMapper>(objectMapper)
            .BuildServiceProvider();

        return new FileAdminAppService(fileRepository, directoryRepository, null!, Substitute.For<IBlobContainerConfigurationProvider>())
        {
            LazyServiceProvider = new AbpLazyServiceProvider(services)
        };
    }

    private static DirectoryAdminAppService CreateDirectoryAppService(IDirectoryDescriptorRepository repository)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Id.Returns((Guid?)null);

        var services = new ServiceCollection()
            .AddSingleton(currentUser)
            .BuildServiceProvider();

        // The user check runs before anything else is touched, so the domain manager is never reached.
        return new DirectoryAdminAppService(null!, repository)
        {
            LazyServiceProvider = new AbpLazyServiceProvider(services)
        };
    }
}
