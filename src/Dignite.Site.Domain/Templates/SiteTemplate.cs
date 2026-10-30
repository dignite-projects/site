using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Templates;

/// <summary>
/// A tenant-authored Scriban template: a header (the name and which version is live) over an append-only
/// list of immutable <see cref="SiteTemplateVersion"/>s.
/// <para>
/// A template is found by <see cref="Name"/>, the same string <c>Page.Template</c> holds: when a page
/// renders, the renderer looks for a <i>published</i> template of that name and falls back to the Razor
/// view of that name when there is none. Editing never overwrites - it adds a version - so a bad edit is
/// undone by publishing the previous version again, and what a site served last week is still on record.
/// </para>
/// </summary>
public class SiteTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    protected SiteTemplate()
    {
        Versions = new List<SiteTemplateVersion>();
    }

    public SiteTemplate(Guid id, string name, Guid? tenantId = null)
        : base(id)
    {
        SetName(name);
        TenantId = tenantId;

        Versions = new List<SiteTemplateVersion>();
    }

    /// <summary>
    /// Unique within the tenant, normalized by <see cref="NormalizeName"/>, and shaped like
    /// <c>Page.Template</c> (<c>Default</c>, <c>Blog/Article</c>) because it is looked up by that value.
    /// </summary>
    public virtual string Name { get; protected set; } = default!;

    /// <summary>
    /// The <see cref="SiteTemplateVersion.Number"/> that is live, or null when the template has never been
    /// published - a draft template, which no page renders.
    /// </summary>
    public virtual int? PublishedVersionNumber { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>Every version ever saved, oldest first by <see cref="SiteTemplateVersion.Number"/>.</summary>
    public virtual ICollection<SiteTemplateVersion> Versions { get; protected set; }

    /// <summary>
    /// Normalizes, validates and assigns the name. A name that fails the shape or length rule of
    /// <c>Page.Template</c> is rejected, since no page could ever refer to it.
    /// </summary>
    public virtual void SetName(string name)
    {
        Check.NotNull(name, nameof(name));

        var normalized = NormalizeName(name);
        if (normalized.Length > SiteTemplateConsts.MaxNameLength ||
            !Regex.IsMatch(normalized, SiteTemplateConsts.NamePattern))
        {
            throw new InvalidValueFormatException(nameof(Name), normalized);
        }

        Name = normalized;
    }

    /// <summary>
    /// The canonical stored form of a name: trimmed, without a leading slash. The same normalization
    /// <c>Page.SetTemplate</c> applies, so a page's template value and a template's name compare equal.
    /// </summary>
    public static string NormalizeName(string name)
    {
        return name.Trim().RemovePreFix("/");
    }

    /// <summary>
    /// Appends a new version, numbered one past the highest existing one, and returns it. Does not publish
    /// it. <b>Requires <see cref="Versions"/> to be loaded</b> (<c>includeDetails: true</c>): the number is
    /// computed from the collection, and on an unloaded one this would hand out number 1 again.
    /// </summary>
    public virtual SiteTemplateVersion AddVersion(Guid id, string body, string? comment)
    {
        var number = (Versions.Count == 0 ? 0 : Versions.Max(v => v.Number)) + 1;
        var version = new SiteTemplateVersion(id, Id, number, body, comment, TenantId);

        Versions.Add(version);
        return version;
    }

    /// <summary>Makes <paramref name="versionNumber"/> the live version - forward or back.</summary>
    /// <exception cref="SiteTemplateVersionNotFoundException">No such version. Also what an unloaded <see cref="Versions"/> looks like.</exception>
    public virtual void Publish(int versionNumber)
    {
        if (FindVersion(versionNumber) == null)
        {
            throw new SiteTemplateVersionNotFoundException(Name, versionNumber);
        }

        PublishedVersionNumber = versionNumber;
    }

    public virtual SiteTemplateVersion? FindVersion(int number)
    {
        return Versions.FirstOrDefault(v => v.Number == number);
    }

    /// <summary>The version with the highest number, or null when there is none yet.</summary>
    public virtual SiteTemplateVersion? GetLatestVersion()
    {
        return Versions.OrderByDescending(v => v.Number).FirstOrDefault();
    }
}
