using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Admin.Files;

[Serializable]
public class FileDescriptorDto : CreationAuditedEntityDto<Guid>, IMultiTenant
{
    public string ContainerName { get; set; } = default!;

    /// <summary>The file's address in its container - the last part of <see cref="Url"/>.</summary>
    public string BlobName { get; set; } = default!;

    public Guid? DirectoryId { get; set; }

    public long Size { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>Detected from the stored bytes.</summary>
    public string MimeType { get; set; } = default!;

    /// <summary>
    /// The address the public read endpoint serves the file at, relative to whichever host serves the site
    /// (<c>/api/site-public/files/{container}/{blob}?__tenant=</c>) - what a file field stores. Not stored on
    /// the file itself: composed from its container, blob name and tenant.
    /// </summary>
    public string? Url { get; set; }

    public Guid? TenantId { get; set; }
}
