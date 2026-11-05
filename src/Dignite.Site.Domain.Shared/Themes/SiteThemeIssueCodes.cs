namespace Dignite.Site.Themes;

/// <summary>How much a check finding matters: an error stops a publish, a warning does not.</summary>
public enum SiteThemeIssueSeverity
{
    Error = 0,
    Warning = 1
}

/// <summary>Where a file of the working copy stands against the live release.</summary>
public enum SiteThemeFileStatus
{
    Unchanged = 0,
    Modified = 1,
    Added = 2,

    /// <summary>In the live release, but no longer in the working copy: publishing removes it.</summary>
    Deleted = 3
}

/// <summary>
/// The findings of a theme check. Each code is also the key of its message in the <c>Site</c> localization
/// resource; the message's <c>{Placeholders}</c> are filled from the finding's data.
/// </summary>
public static class SiteThemeIssueCodes
{
    private const string Prefix = "ThemeIssue:";

    // Errors: a publish is refused while any is found.

    /// <summary>The template does not parse. Data: Message.</summary>
    public const string SyntaxError = Prefix + "SyntaxError";

    /// <summary>The front matter sets something other than <c>layout = "path"</c>. Data: Message.</summary>
    public const string FrontMatterInvalid = Prefix + "FrontMatterInvalid";

    /// <summary>A file used as a layout has front matter of its own; a layout cannot choose a layout.</summary>
    public const string FrontMatterInLayout = Prefix + "FrontMatterInLayout";

    /// <summary>The default layout setting names no template of the working copy. Data: Layout.</summary>
    public const string DefaultLayoutMissing = Prefix + "DefaultLayoutMissing";

    /// <summary>An <c>include</c>, <c>asset_url</c> or <c>layout</c> names no file. Data: Function, Target.</summary>
    public const string ReferenceMissing = Prefix + "ReferenceMissing";

    /// <summary>An <c>include</c> or a <c>layout</c> names a static file. Data: Function, Target.</summary>
    public const string ReferenceToStaticFile = Prefix + "ReferenceToStaticFile";

    /// <summary>An <c>asset_url</c> names a template. Data: Target.</summary>
    public const string AssetUrlToTemplate = Prefix + "AssetUrlToTemplate";

    /// <summary>Templates include each other in a circle. Data: Chain.</summary>
    public const string IncludeCycle = Prefix + "IncludeCycle";

    /// <summary>
    /// An <c>include</c> does not pass a named argument the template reads as a local (<c>$title</c>), which fails the
    /// render. Reported at the <c>include</c>. Data: Target, Variable.
    /// </summary>
    public const string IncludeArgumentMissing = Prefix + "IncludeArgumentMissing";

    /// <summary>A file used as a layout calls <c>site_head</c> nowhere, not even through what it includes.</summary>
    public const string LayoutWithoutSiteHead = Prefix + "LayoutWithoutSiteHead";

    /// <summary>A page template rendered without a layout calls <c>site_head</c> nowhere.</summary>
    public const string PageWithoutSiteHead = Prefix + "PageWithoutSiteHead";

    /// <summary>A file in <c>locales/</c> is not a valid file of the theme's texts. Data: Message.</summary>
    public const string TextFileInvalid = Prefix + "TextFileInvalid";

    /// <summary>Two files in <c>locales/</c> hold the texts of the same language. Data: Culture, Other (the file that came first).</summary>
    public const string TextFileDuplicate = Prefix + "TextFileDuplicate";

    /// <summary>A language version (<c>Default.ja</c>) has no template (<c>Default</c>) to be the version of. Data: Template.</summary>
    public const string LanguageVersionWithoutTemplate = Prefix + "LanguageVersionWithoutTemplate";

    /// <summary>
    /// Rendering a sample page with the working copy failed - a template error only a render finds. Reported at the
    /// template and place the message names, else at the page's template as a whole. Data: Path (the sample address
    /// that failed), Message.
    /// </summary>
    public const string RenderFailed = Prefix + "RenderFailed";

    // Warnings: worth a look, but nothing breaks.

    /// <summary>
    /// A rendered page refers to <c>#id</c> and nothing on it has that id - a <c>&lt;use&gt;</c> of a symbol that is not
    /// there, an anchor to a section that does not exist. Found in the trial render, not in a template: the id may be
    /// defined by a layout or another partial. Data: Path (the first sample address that has it), Id.
    /// </summary>
    public const string FragmentReferenceNotFound = Prefix + "FragmentReferenceNotFound";

    /// <summary>The trial render stopped before the whole sample was rendered, for the time it took. Data: Rendered, Total.</summary>
    public const string TrialRenderIncomplete = Prefix + "TrialRenderIncomplete";

    /// <summary>
    /// The trial render rendered nothing: the application has no renderer, as an administration API deployed apart from
    /// <c>Dignite.Site.Public.Web</c> has none. Only when the theme renders some page. Data: Total (the sample addresses).
    /// </summary>
    public const string TrialRenderUnavailable = Prefix + "TrialRenderUnavailable";

    /// <summary>Pages whose template is not in the theme; a Razor view renders them. Data: Template, Pages.</summary>
    public const string PageRenderedByRazor = Prefix + "PageRenderedByRazor";

    /// <summary>A file used as a layout never writes <c>{{ body }}</c>, so the page's own output is lost.</summary>
    public const string LayoutWithoutBody = Prefix + "LayoutWithoutBody";

    /// <summary>A file used as a layout is also a page template or included. Data: Roles.</summary>
    public const string MixedRoles = Prefix + "MixedRoles";

    /// <summary>No page, template or setting refers to the file.</summary>
    public const string UnusedFile = Prefix + "UnusedFile";
}
