using Volo.Abp;

namespace Dignite.Site.Pages;

/// <summary>
/// A page with children cannot be deleted. <see cref="PageManager.DeleteAsync"/> already cascades to
/// every content type and content beneath the page itself - stacking a cascade to child pages, each
/// carrying its own content types and contents, would multiply the blast radius of one delete far past
/// what a confirmation dialog can convey. Move or delete the children first.
/// </summary>
public class PageHasChildrenException : BusinessException
{
    public PageHasChildrenException(string name)
        : base(SiteErrorCodes.PageHasChildren)
    {
        WithData("Name", name);
    }
}
