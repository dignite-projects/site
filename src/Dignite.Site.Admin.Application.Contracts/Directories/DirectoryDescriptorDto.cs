using System;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Directories;

public class DirectoryDescriptorDto : ExtensibleAuditedEntityDto<Guid>
{
    public string ContainerName { get; set; } = default!;

    public string Name { get; set; } = default!;

    public Guid? ParentId { get; set; }

    public int Order { get; set; }
}
