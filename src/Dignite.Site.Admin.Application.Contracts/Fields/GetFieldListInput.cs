using System;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Fields;

public class GetFieldListInput : PagedAndSortedResultRequestDto
{
    public Guid? GroupId { get; set; }

    public string? Filter { get; set; }
}
