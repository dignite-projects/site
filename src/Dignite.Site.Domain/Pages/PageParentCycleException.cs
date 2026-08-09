using Volo.Abp;

namespace Dignite.Site.Pages;

/// <summary>
/// A page cannot be organized under itself or under one of its own descendants - walking the candidate
/// parent's own chain back up would never reach a root.
/// </summary>
public class PageParentCycleException : BusinessException
{
    public PageParentCycleException(string name)
        : base(SiteErrorCodes.PageParentCycle)
    {
        WithData("Name", name);
    }
}
