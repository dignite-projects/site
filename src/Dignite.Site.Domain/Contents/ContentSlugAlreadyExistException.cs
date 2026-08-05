using Volo.Abp;

namespace Dignite.Site.Contents;

/// <summary>
/// Violates <c>(TenantId, PageId, CultureName, Slug)</c> uniqueness (总体设计 §2.5). Two contents sharing
/// a slug under one page and language would make the URL that resolves to them ambiguous.
/// </summary>
public class ContentSlugAlreadyExistException : BusinessException
{
    public ContentSlugAlreadyExistException(string cultureName, string slug)
        : base(SiteErrorCodes.ContentSlugAlreadyExists)
    {
        WithData("CultureName", cultureName);
        WithData("Slug", slug);
    }
}
