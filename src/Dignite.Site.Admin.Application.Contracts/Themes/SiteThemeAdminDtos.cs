using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Site.Themes;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Themes;

/// <summary>The theme's settings and which release is live.</summary>
public class SiteThemeDto
{
    /// <summary>The working copy's default layout: the layout of a page template whose front matter names none.</summary>
    public string? DefaultLayout { get; set; }

    public Guid? LiveReleaseId { get; set; }

    public int? LiveReleaseNumber { get; set; }

    /// <summary>Send it back with <c>UpdateAsync</c>; a change made by someone else in between is then a conflict.</summary>
    public string ConcurrencyStamp { get; set; } = default!;
}

public class UpdateSiteThemeDto
{
    /// <summary>A template path, or null/empty for no default layout.</summary>
    [DynamicStringLength(typeof(SiteThemeConsts), nameof(SiteThemeConsts.MaxPathLength))]
    public string? DefaultLayout { get; set; }

    public string? ConcurrencyStamp { get; set; }
}

/// <summary>A file of the working copy without its body, and how it stands against the live release.</summary>
public class SiteThemeFileSummaryDto
{
    public string Path { get; set; } = default!;

    /// <summary>Whether it is a static file (its name has an extension) rather than a Scriban template.</summary>
    public bool IsAsset { get; set; }

    public string ContentHash { get; set; } = default!;

    /// <summary>
    /// Against the live release. <see cref="SiteThemeFileStatus.Deleted"/> lists a file the live release has and the
    /// working copy no longer does.
    /// </summary>
    public SiteThemeFileStatus Status { get; set; }

    /// <summary>When the file was last saved; null for a deleted one.</summary>
    public DateTime? LastModificationTime { get; set; }
}

/// <summary>A file of the working copy with its body.</summary>
public class SiteThemeFileDto
{
    public string Path { get; set; } = default!;

    public bool IsAsset { get; set; }

    public string Body { get; set; } = default!;

    public string ContentHash { get; set; } = default!;

    /// <summary>Send it back with <c>SaveFileAsync</c>; a save by someone else in between is then a conflict.</summary>
    public string ConcurrencyStamp { get; set; } = default!;

    public DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// The template's syntax errors - a file that does not parse is saved all the same, and listed here; a publish
    /// is refused while any remain. Empty for a static file.
    /// </summary>
    public List<SiteThemeSyntaxErrorDto> SyntaxErrors { get; set; } = new();
}

public class SiteThemeSyntaxErrorDto
{
    public int Line { get; set; }

    public int Column { get; set; }

    public string Message { get; set; } = default!;
}

public class SaveSiteThemeFileDto
{
    /// <summary>
    /// The file's path, e.g. <c>Events</c>, <c>blog/_sidebar</c> or <c>css/site.css</c>. Without an extension it is
    /// a Scriban template; with one it is a static file, whose extension must be one the site serves.
    /// </summary>
    [Required]
    [DynamicStringLength(typeof(SiteThemeConsts), nameof(SiteThemeConsts.MaxPathLength))]
    public string Path { get; set; } = default!;

    [Required(AllowEmptyStrings = true)]
    [DynamicStringLength(typeof(SiteThemeConsts), nameof(SiteThemeConsts.MaxBodyLength))]
    public string Body { get; set; } = default!;

    /// <summary>The stamp the file was read with. Omit to create a file, or to overwrite one whatever it holds now.</summary>
    public string? ConcurrencyStamp { get; set; }
}

public class RenameSiteThemeFileDto
{
    [Required]
    [DynamicStringLength(typeof(SiteThemeConsts), nameof(SiteThemeConsts.MaxPathLength))]
    public string Path { get; set; } = default!;

    [Required]
    [DynamicStringLength(typeof(SiteThemeConsts), nameof(SiteThemeConsts.MaxPathLength))]
    public string NewPath { get; set; } = default!;

    /// <summary>
    /// Whether to rewrite every literal <c>include</c>, <c>asset_url</c> and front matter <c>layout</c> that names the old
    /// path, and the default layout setting. Pages whose <c>Template</c> names it are never changed.
    /// </summary>
    public bool UpdateReferences { get; set; } = true;
}

public class SiteThemeRenameResultDto
{
    public SiteThemeFileDto File { get; set; } = default!;

    /// <summary>The files whose references were rewritten.</summary>
    public List<string> UpdatedPaths { get; set; } = new();

    /// <summary>Whether the default layout setting named the old path and now names the new one.</summary>
    public bool DefaultLayoutUpdated { get; set; }

    /// <summary>The pages whose <c>Template</c> still names the old path: they now render through a Razor view of that name.</summary>
    public List<string> PagesStillUsingOldPath { get; set; } = new();
}

/// <summary>Who uses a file: what to know before deleting or renaming it.</summary>
public class SiteThemeFileUsagesDto
{
    public string Path { get; set; } = default!;

    public List<SiteThemeFileUsageDto> Usages { get; set; } = new();
}

public class SiteThemeFileUsageDto
{
    public SiteThemeUsageKindDto Kind { get; set; }

    /// <summary>The file that refers to it, for an include, an asset_url or a layout.</summary>
    public string? SourcePath { get; set; }

    /// <summary>The page whose Template names it.</summary>
    public string? PageName { get; set; }
}

/// <summary>Mirrors the domain's usage kinds, so the contract does not depend on the domain layer.</summary>
public enum SiteThemeUsageKindDto
{
    Page = 0,
    Include = 1,
    AssetUrl = 2,
    Layout = 3,
    DefaultLayout = 4
}

/// <summary>The result of checking the working copy: what is wrong, what would change, and the fingerprint a publish must bring back.</summary>
public class SiteThemeCheckDto
{
    /// <summary>Pass it to <c>PublishAsync</c>: a publish is refused when the working copy changed since.</summary>
    public string Fingerprint { get; set; } = default!;

    /// <summary>Errors first, then warnings.</summary>
    public List<SiteThemeIssueDto> Issues { get; set; } = new();

    public bool HasErrors { get; set; }

    /// <summary>
    /// The templates that can serve as a layout - they write <c>body</c>, call <c>site_head</c> and have no front
    /// matter - and so can be the default layout. Read off the saved files, whether or not anything uses them yet.
    /// </summary>
    public List<string> Layouts { get; set; } = new();

    /// <summary>The files that differ from the live release; unchanged ones are left out.</summary>
    public List<SiteThemeFileChangeDto> Changes { get; set; } = new();

    public string? DefaultLayout { get; set; }

    /// <summary>The live release's default layout, to show beside <see cref="DefaultLayout"/> when they differ.</summary>
    public string? LiveDefaultLayout { get; set; }

    public int? LiveReleaseNumber { get; set; }

    /// <summary>Whether publishing would change anything.</summary>
    public bool HasChanges { get; set; }

    /// <summary>
    /// Which pages this check rendered: none, those a file affects, or the whole sample. Less than asked for when the
    /// reading found an error - a theme that does not read cleanly is not rendered at all.
    /// </summary>
    public SiteThemeRenderScope Render { get; set; }

    /// <summary>
    /// The page templates (language versions apart: <c>Blog/Post.ja</c>) whose every sample address this check rendered.
    /// The findings of an earlier render for any other template still stand, as far as this check can tell.
    /// </summary>
    public List<string> RenderedTemplates { get; set; } = new();
}

/// <summary>How many of the site's pages a check renders after reading the theme.</summary>
public enum SiteThemeRenderScope
{
    /// <summary>A sample of every page the theme renders. What a publish needs.</summary>
    All = 0,

    /// <summary>No page: the reading alone, which takes a moment where rendering can take seconds.</summary>
    None = 1,

    /// <summary>
    /// The pages <see cref="SiteThemeCheckInput.ChangedPath"/> takes part in rendering - as their template, their layout
    /// or a partial either includes. A file of the theme's texts takes part in every page; a static file in none.
    /// </summary>
    Affected = 2
}

public class SiteThemeCheckInput
{
    public SiteThemeRenderScope Render { get; set; } = SiteThemeRenderScope.All;

    /// <summary>The file that changed, for <see cref="SiteThemeRenderScope.Affected"/>; without one, every page is rendered.</summary>
    [DynamicStringLength(typeof(SiteThemeConsts), nameof(SiteThemeConsts.MaxPathLength))]
    public string? ChangedPath { get; set; }
}

public class SiteThemeIssueDto
{
    public SiteThemeIssueSeverity Severity { get; set; }

    /// <summary>A <see cref="SiteThemeIssueCodes"/> value.</summary>
    public string Code { get; set; } = default!;

    /// <summary>The file it is about; null for the theme as a whole (the default layout setting, a page's template).</summary>
    public string? Path { get; set; }

    public int? Line { get; set; }

    public int? Column { get; set; }

    /// <summary>The finding in the user's language.</summary>
    public string Message { get; set; } = default!;

    /// <summary>Whether rendering the pages found it, rather than reading the files.</summary>
    public bool FoundByRender { get; set; }

    /// <summary>
    /// For a finding of the render about pages: the page templates whose sample addresses met it - all of them, though
    /// the finding is listed once. Empty for a finding of the reading, and for one about the render as a whole.
    /// </summary>
    public List<string> Templates { get; set; } = new();
}

public class SiteThemeFileChangeDto
{
    public string Path { get; set; } = default!;

    public SiteThemeFileStatus Status { get; set; }
}

public class PublishSiteThemeDto
{
    /// <summary>From <c>CheckAsync</c>.</summary>
    [Required]
    public string Fingerprint { get; set; } = default!;

    [DynamicStringLength(typeof(SiteThemeConsts), nameof(SiteThemeConsts.MaxCommentLength))]
    public string? Comment { get; set; }
}

public class SiteReleaseDto
{
    public Guid Id { get; set; }

    public int Number { get; set; }

    public string? Comment { get; set; }

    public string? DefaultLayout { get; set; }

    /// <summary>How many files it added, changed or removed against the release live before it.</summary>
    public int ChangedFileCount { get; set; }

    public DateTime CreationTime { get; set; }

    public Guid? CreatorId { get; set; }

    /// <summary>Whether the site renders it now.</summary>
    public bool IsLive { get; set; }
}

public class SiteReleaseDetailDto : SiteReleaseDto
{
    public List<SiteReleaseFileDto> Files { get; set; } = new();
}

public class SiteReleaseFileDto
{
    public string Path { get; set; } = default!;

    public bool IsAsset { get; set; }

    public string ContentHash { get; set; } = default!;
}

public class SiteReleaseFileContentDto : SiteReleaseFileDto
{
    public string Body { get; set; } = default!;
}
