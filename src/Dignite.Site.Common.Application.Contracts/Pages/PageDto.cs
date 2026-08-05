using System;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Pages;

/// <summary>
/// A page, read-shaped. Shared between the Admin and Public application services - both read the same
/// routing-table row, only Admin can write it (总体设计 §2.2).
/// </summary>
public class PageDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string Route { get; set; } = default!;

    public string? ContentPathPattern { get; set; }

    public string? Template { get; set; }

    public bool IsHomePage { get; set; }

    public int Order { get; set; }

    public bool IsActive { get; set; }
}
