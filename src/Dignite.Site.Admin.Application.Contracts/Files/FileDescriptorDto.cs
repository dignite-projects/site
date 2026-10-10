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
    /// The absolute address the public read endpoint serves the file at
    /// (<c>/api/site-public/files/{container}/{blob}?__tenant=</c>). Not stored: the HTTP API and the MCP
    /// tools fill it in from the request they answer.
    /// </summary>
    public string? Url { get; set; }

    public Guid? TenantId { get; set; }
}
