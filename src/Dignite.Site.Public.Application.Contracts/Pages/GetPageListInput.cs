using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Pages;

/// <summary>No <c>IsActive</c> filter - the service always forces active-only (总体设计 §2.2).</summary>
public class GetPageListInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
