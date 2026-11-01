using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Themes;

/// <summary>
/// A tenant's theme: the settings of its working copy, and which release the site renders. The working copy's
/// files are <see cref="SiteThemeFile"/>s, each its own aggregate so that saving one file touches one row; the
/// releases are <see cref="SiteRelease"/>s.
/// <para>
/// One per tenant for now. The theme is an entity of its own rather than columns on a tenant so that several
/// themes per tenant - a copy to try a redesign in - can come later without moving data.
/// </para>
/// </summary>
public class SiteTheme : AuditedAggregateRoot<Guid>, IMultiTenant
{
    protected SiteTheme()
    {
    }

    public SiteTheme(Guid id, Guid? tenantId = null, string? defaultLayout = SiteThemeConsts.InitialDefaultLayout)
        : base(id)
    {
        TenantId = tenantId;
        SetDefaultLayout(defaultLayout);
    }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>
    /// The layout a page template uses when its front matter names none - a template path, or null for no layout.
    /// The working copy's value; the live one is the release's <see cref="SiteRelease.DefaultLayout"/>, so that
    /// changing it is published, previewed and rolled back together with the files.
    /// </summary>
    public virtual string? DefaultLayout { get; protected set; }

    /// <summary>The release the site renders, or null when nothing has been published yet.</summary>
    public virtual Guid? LiveReleaseId { get; protected set; }

    /// <summary>Sets <see cref="DefaultLayout"/>: a template path, normalized, or null/empty for none.</summary>
    /// <exception cref="BusinessException"><see cref="SiteErrorCodes.SiteThemeDefaultLayoutInvalid"/>: not a template path.</exception>
    public virtual void SetDefaultLayout(string? defaultLayout)
    {
        if (string.IsNullOrWhiteSpace(defaultLayout))
        {
            DefaultLayout = null;
            return;
        }

        var path = SiteThemePaths.Normalize(defaultLayout);
        if (!SiteThemePaths.IsValid(path) || !SiteThemePaths.IsTemplate(path))
        {
            throw new BusinessException(SiteErrorCodes.SiteThemeDefaultLayoutInvalid).WithData("Layout", path);
        }

        DefaultLayout = path;
    }

    /// <summary>Makes <paramref name="releaseId"/> the release the site renders - a new one, or an older one to roll back.</summary>
    public virtual void SetLiveRelease(Guid releaseId)
    {
        LiveReleaseId = releaseId;
    }
}
