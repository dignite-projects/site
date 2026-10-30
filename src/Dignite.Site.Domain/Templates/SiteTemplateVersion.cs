using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Templates;

/// <summary>
/// One saved state of a <see cref="SiteTemplate"/>'s source. Immutable by design: there is no setter and no
/// "edit version" operation, because a published version is what live pages render and the record of what
/// was live must not change under it. Changing a template means adding the next version.
/// </summary>
public class SiteTemplateVersion : CreationAuditedEntity<Guid>, IMultiTenant
{
    protected SiteTemplateVersion()
    {
    }

    public SiteTemplateVersion(
        Guid id,
        Guid templateId,
        int number,
        string body,
        string? comment,
        Guid? tenantId = null)
        : base(id)
    {
        TemplateId = templateId;
        Number = number;
        // Not NotNullOrWhiteSpace: an empty template is legal (it renders as nothing).
        Body = Check.NotNull(body, nameof(body), maxLength: SiteTemplateConsts.MaxBodyLength);
        Comment = Check.Length(comment, nameof(comment), SiteTemplateConsts.MaxCommentLength);
        TenantId = tenantId;
    }

    public virtual Guid TemplateId { get; protected set; }

    /// <summary>1-based, unique within the template, never reused.</summary>
    public virtual int Number { get; protected set; }

    /// <summary>The Scriban source.</summary>
    public virtual string Body { get; protected set; } = default!;

    /// <summary>An optional note on what this version changed.</summary>
    public virtual string? Comment { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }
}
