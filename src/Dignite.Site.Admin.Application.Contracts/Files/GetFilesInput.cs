using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Files;

public class GetFilesInput : PagedAndSortedResultRequestDto
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>Only the files directly in this directory; <c>null</c> lists every directory.</summary>
    public Guid? DirectoryId { get; set; }

    /// <summary>
    /// Only this user's files. Ignored without <c>SiteAdmin.Contents</c>: such a caller always gets only
    /// their own.
    /// </summary>
    public Guid? CreatorId { get; set; }

    /// <summary>Matched against the file name.</summary>
    public string? Filter { get; set; }
}
