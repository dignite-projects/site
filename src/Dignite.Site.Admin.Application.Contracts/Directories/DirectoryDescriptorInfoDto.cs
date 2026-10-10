using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Directories;

/// <summary>A node of the caller's directory tree (<see cref="IDirectoryAdminAppService.GetListAsync"/>).</summary>
public class DirectoryDescriptorInfoDto : ExtensibleEntityDto<Guid>, IEquatable<DirectoryDescriptorInfoDto>
{
    public DirectoryDescriptorInfoDto()
    {
        Children = new ObservableCollection<DirectoryDescriptorInfoDto>();
    }

    public string ContainerName { get; set; } = default!;

    public string Name { get; set; } = default!;

    public Guid? ParentId { get; set; }

    public int Order { get; set; }

    public ObservableCollection<DirectoryDescriptorInfoDto> Children { get; set; }

    public bool Equals(DirectoryDescriptorInfoDto? other)
    {
        return other != null && Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as DirectoryDescriptorInfoDto);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public void AddChild(DirectoryDescriptorInfoDto child)
    {
        child.ParentId = Id;
        Children.Add(child);
    }

    public void RemoveChild(DirectoryDescriptorInfoDto child)
    {
        Children.RemoveAll(c => c.Id == child.Id);
    }
}
