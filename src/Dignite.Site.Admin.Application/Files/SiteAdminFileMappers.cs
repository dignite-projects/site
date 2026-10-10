using Dignite.Site.Admin.Directories;
using Dignite.Site.Directories;
using Dignite.Site.Files;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// <see cref="FileDescriptor"/> -&gt; <see cref="FileDescriptorDto"/>. <c>Url</c> is not stored: the HTTP API
/// and the MCP tools fill it in from the request they answer (<c>SiteFileUrl.Build</c>).
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FileDescriptorToDtoMapper : MapperBase<FileDescriptor, FileDescriptorDto>
{
    [MapperIgnoreTarget(nameof(FileDescriptorDto.Url))]
    public override partial FileDescriptorDto Map(FileDescriptor source);

    [MapperIgnoreTarget(nameof(FileDescriptorDto.Url))]
    public override partial void Map(FileDescriptor source, FileDescriptorDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
[MapExtraProperties]
public partial class DirectoryDescriptorToDtoMapper : MapperBase<DirectoryDescriptor, DirectoryDescriptorDto>
{
    public override partial DirectoryDescriptorDto Map(DirectoryDescriptor source);

    public override partial void Map(DirectoryDescriptor source, DirectoryDescriptorDto destination);
}

/// <summary>
/// <see cref="DirectoryDescriptor"/> -&gt; <see cref="DirectoryDescriptorInfoDto"/>. <c>Children</c> is the
/// tree, assembled by <c>DirectoryListExtensions.BuildTree</c> rather than mapped from the entity.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
[MapExtraProperties]
public partial class DirectoryDescriptorToInfoDtoMapper : MapperBase<DirectoryDescriptor, DirectoryDescriptorInfoDto>
{
    [MapperIgnoreTarget(nameof(DirectoryDescriptorInfoDto.Children))]
    public override partial DirectoryDescriptorInfoDto Map(DirectoryDescriptor source);

    [MapperIgnoreTarget(nameof(DirectoryDescriptorInfoDto.Children))]
    public override partial void Map(DirectoryDescriptor source, DirectoryDescriptorInfoDto destination);
}
