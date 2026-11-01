using System;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Themes;

/// <summary>
/// A published file body, stored once per tenant and content: releases refer to it by
/// <see cref="ContentHash"/>. Written only when publishing, so every blob is content that was live at some point,
/// and the asset endpoint, which reads nothing else, can never hand out a draft. Never changed, never deleted with a
/// release (a release is never deleted).
/// </summary>
public class SiteThemeBlob : BasicAggregateRoot<Guid>, IMultiTenant, IHasCreationTime
{
    protected SiteThemeBlob()
    {
    }

    public SiteThemeBlob(Guid id, string contentHash, string body, Guid? tenantId = null)
        : base(id)
    {
        ContentHash = Check.NotNullOrWhiteSpace(contentHash, nameof(contentHash), SiteThemeConsts.ContentHashLength);
        Body = Check.NotNull(body, nameof(body));
        TenantId = tenantId;
    }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary><see cref="SiteThemePaths.ComputeContentHash"/> of <see cref="Body"/>; unique per tenant.</summary>
    public virtual string ContentHash { get; protected set; } = default!;

    public virtual string Body { get; protected set; } = default!;

    public virtual DateTime CreationTime { get; protected set; }
}
