using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Themes;

/// <summary>
/// One file of a theme's working copy: a Scriban template or a static file, by its path
/// (<see cref="SiteThemePaths"/>). Saved in place - no version is kept per save; what was live is kept by the
/// <see cref="SiteRelease"/>s instead. Its own aggregate, so saving a file is one row and two people editing two
/// files never conflict; two people saving the same file do, through the concurrency stamp.
/// <para>
/// A body that does not parse may be saved: the working copy is the author's own, and refusing a save would only
/// lose what they wrote. A publish checks every file and refuses errors.
/// </para>
/// </summary>
public class SiteThemeFile : AuditedAggregateRoot<Guid>, IMultiTenant
{
    protected SiteThemeFile()
    {
    }

    public SiteThemeFile(Guid id, Guid themeId, string path, string body, Guid? tenantId = null)
        : base(id)
    {
        ThemeId = themeId;
        TenantId = tenantId;
        SetPath(path);
        SetBody(body);
    }

    public virtual Guid ThemeId { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>Unique within the theme, normalized (<see cref="SiteThemePaths.Normalize"/>).</summary>
    public virtual string Path { get; protected set; } = default!;

    /// <summary>The Scriban source, or the static file's text.</summary>
    public virtual string Body { get; protected set; } = default!;

    /// <summary><see cref="SiteThemePaths.ComputeContentHash"/> of <see cref="Body"/>, kept with it.</summary>
    public virtual string ContentHash { get; protected set; } = default!;

    /// <summary>Whether the file is a static file rather than a template - see <see cref="SiteThemePaths.IsAsset"/>.</summary>
    public virtual bool IsAsset => SiteThemePaths.IsAsset(Path);

    /// <summary>
    /// Normalizes, validates and assigns the path.
    /// </summary>
    /// <exception cref="InvalidValueFormatException">Not the shape of a path.</exception>
    /// <exception cref="BusinessException"><see cref="SiteErrorCodes.SiteThemeFileExtensionNotAllowed"/>: an extension no static file may have.</exception>
    public virtual void SetPath(string path)
    {
        Check.NotNull(path, nameof(path));

        var normalized = SiteThemePaths.Normalize(path);
        if (!SiteThemePaths.IsValid(normalized))
        {
            throw new InvalidValueFormatException(nameof(Path), normalized);
        }

        if (!SiteThemePaths.IsAllowed(normalized))
        {
            throw new BusinessException(SiteErrorCodes.SiteThemeFileExtensionNotAllowed)
                .WithData("Path", normalized)
                .WithData("Extension", SiteThemePaths.GetExtension(normalized) ?? string.Empty);
        }

        Path = normalized;
    }

    public virtual void SetBody(string body)
    {
        // Not NotNullOrWhiteSpace: an empty file is legal (a template that renders nothing, an empty stylesheet).
        Body = Check.NotNull(body, nameof(body), maxLength: SiteThemeConsts.MaxBodyLength);
        ContentHash = SiteThemePaths.ComputeContentHash(Body);
    }
}
