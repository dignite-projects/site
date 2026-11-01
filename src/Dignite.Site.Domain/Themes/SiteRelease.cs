using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Themes;

/// <summary>
/// A published state of the whole theme: every file's path and content hash at the moment of publishing, and the
/// default layout setting. Immutable - there is no operation that changes one - so the site renders exactly what
/// was checked and published, a rollback is pointing <see cref="SiteTheme.LiveReleaseId"/> at an older one, and
/// anything derived from a release (parsed templates, an asset's address) can be cached by its id for good.
/// <para>
/// The bodies are not here: <see cref="SiteThemeBlob"/> keeps each distinct content once per tenant, so a release
/// that changes one file of fifty stores one new body.
/// </para>
/// </summary>
public class SiteRelease : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    protected SiteRelease()
    {
        Files = new List<SiteReleaseFile>();
    }

    public SiteRelease(
        Guid id,
        Guid themeId,
        int number,
        string? defaultLayout,
        string? comment,
        int changedFileCount,
        IEnumerable<(string Path, string ContentHash)> files,
        Guid? tenantId = null)
        : base(id)
    {
        ThemeId = themeId;
        Number = number;
        DefaultLayout = defaultLayout;
        Comment = Check.Length(comment, nameof(comment), SiteThemeConsts.MaxCommentLength);
        ChangedFileCount = changedFileCount;
        TenantId = tenantId;
        Files = files.Select(file => new SiteReleaseFile(id, file.Path, file.ContentHash)).ToList();
    }

    public virtual Guid ThemeId { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>1-based within the theme, never reused.</summary>
    public virtual int Number { get; protected set; }

    /// <summary>The default layout setting as it was published; see <see cref="SiteTheme.DefaultLayout"/>.</summary>
    public virtual string? DefaultLayout { get; protected set; }

    /// <summary>An optional note on what this release changed.</summary>
    public virtual string? Comment { get; protected set; }

    /// <summary>
    /// How many files this release added, changed or removed against the release that was live when it was
    /// published - worked out once, when publishing, so a history does not have to compare manifests.
    /// </summary>
    public virtual int ChangedFileCount { get; protected set; }

    /// <summary>Every file of the theme at publishing time, by path.</summary>
    public virtual ICollection<SiteReleaseFile> Files { get; protected set; }
}

/// <summary>One file of a <see cref="SiteRelease"/>: its path and which content it had.</summary>
public class SiteReleaseFile : Entity
{
    protected SiteReleaseFile()
    {
    }

    public SiteReleaseFile(Guid releaseId, string path, string contentHash)
    {
        ReleaseId = releaseId;
        Path = path;
        ContentHash = contentHash;
    }

    public virtual Guid ReleaseId { get; protected set; }

    public virtual string Path { get; protected set; } = default!;

    /// <summary>The <see cref="SiteThemeBlob.ContentHash"/> of its body.</summary>
    public virtual string ContentHash { get; protected set; } = default!;

    public override object[] GetKeys()
    {
        return new object[] { ReleaseId, Path };
    }
}
