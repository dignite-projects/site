using Volo.Abp.Application.Dtos;

namespace Dignite.Sites.Admin.Pages;

public class GetPageListInput : PagedAndSortedResultRequestDto
{
    public bool? IsActive { get; set; }

    public string? Filter { get; set; }
}
