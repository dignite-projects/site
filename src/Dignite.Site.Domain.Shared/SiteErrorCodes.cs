namespace Dignite.Site;

/// <summary>
/// Error codes for the module's business exceptions. Each one is looked up in the <c>Site</c>
/// localization resource by this exact string - <c>SiteDomainSharedModule</c> maps the <c>Site</c>
/// namespace to <c>SiteResource</c> - so a code added here needs a matching entry in
/// <c>Localization/Site/*.json</c> or the raw code surfaces to the caller.
/// </summary>
public static class SiteErrorCodes
{
    public const string PageRouteAlreadyExists = "Site:010001";
    public const string PageNameAlreadyExists = "Site:010002";
    public const string InvalidPageRoute = "Site:010003";
    public const string PageParentCycle = "Site:010004";
    public const string PageHasChildren = "Site:010005";

    public const string ContentTypeNameAlreadyExists = "Site:020001";
    public const string ContentTypeFieldDuplicated = "Site:020002";

    public const string FieldNameAlreadyExists = "Site:030001";
    public const string FieldIsPlatformPresetCannotBeDeleted = "Site:030002";
    public const string FieldIsPlatformPresetCannotBeRenamed = "Site:030003";
    public const string FieldNestingTooDeep = "Site:030004";

    public const string ContentSlugAlreadyExists = "Site:040001";
    public const string ContentPageInconsistent = "Site:040002";
    public const string ContentDraftCannotHaveFuturePublishTime = "Site:040003";
    public const string ContentSlugNotAllowed = "Site:040004";
    public const string ContentSlugRequired = "Site:040005";
    public const string ContentSlugNotMatchingRoute = "Site:040006";
    public const string ContentCultureNotRecognized = "Site:040007";

    public const string PrimaryDomainNotConfigured = "Site:050001";

    /// <summary>
    /// Shared across every "shaped identifier" property (Page.Name/Route/Template, ContentType.Name,
    /// Field.Name, Content.Slug) rather than one code per property - the recovery is always the same
    /// (fix the value's characters), so the reason does not need its own number per field.
    /// </summary>
    public const string InvalidValueFormat = "Site:060001";

    // The file library's directories (Dignite.Site.Files). A directory belongs to the user who
    // created it, so "not found" also answers for another user's or another tenant's directory.
    public const string DirectoryNameAlreadyExists = "Site:070001";
    public const string DirectoryNotFound = "Site:070002";
    public const string DirectoryCannotMoveAcrossContainers = "Site:070003";
    public const string DirectoryCannotMoveIntoItself = "Site:070004";
    public const string DirectoryHasChildren = "Site:070005";
    public const string DirectoryHasFiles = "Site:070006";
    public const string DirectoryRequiresUser = "Site:070007";

    // The file library's files (Dignite.Site.Files). Upload rules - size, type, image bounds - are the
    // codes of Dignite.Abp.BlobStoring.Pipeline (Dignite.Abp.BlobStoring:*, raised by its contributors and
    // IMimeTypeDetector) and Dignite.Abp.BlobStoring.Imaging (Dignite.Abp.BlobStoring.Imaging:*), localized
    // by those packages; the public read endpoint's on-request resize reuses the Imaging codes.
    public const string FileContainerNotAvailable = "Site:080001";
    public const string FileSortingNotSupported = "Site:080002";

    /// <summary>An upload link that is not one this site issued, has expired, or has been used. One code for all three.</summary>
    public const string FileUploadTicketInvalid = "Site:080003";

    // The tenant's theme (Dignite.Site.Themes): a working copy of files, published as immutable releases.
    public const string SiteThemeFileAlreadyExists = "Site:090001";

    /// <summary>A publish refused because the check found errors; Data["Count"] is how many.</summary>
    public const string SiteThemeHasErrors = "Site:090002";

    /// <summary>A publish whose fingerprint is not the working copy's any more: someone saved in between.</summary>
    public const string SiteThemeChangedSinceCheck = "Site:090003";

    /// <summary>A file whose extension is not a static file the site serves; Data["Path"].</summary>
    public const string SiteThemeFileExtensionNotAllowed = "Site:090004";

    /// <summary>
    /// Raised by the renderer, which cannot reference Domain, as a plain BusinessException carrying this code:
    /// a page rendered without calling <c>site_head</c>.
    /// </summary>
    public const string SiteTemplateDidNotRenderSiteHead = "Site:090005";

    // Previewing a page of the site with the working copy or a release. The first is raised by the renderer in
    // Dignite.Site.Public.Web (no Domain there either), the second by the admin service when there is no renderer - an
    // administration API hosted without Dignite.Site.Public.Web and without RemoteServices:SitePublicWeb.
    public const string SiteTemplatePreviewPathNotFound = "Site:090006";
    public const string SiteTemplatePreviewUnavailable = "Site:090007";

    // A preview whose template failed at run time or ran out of time, raised by the same renderer. The
    // engine's own exception would reach an HTTP or MCP caller as "an internal error occurred"; this code
    // carries its message - template, line and column - as Data["Message"].
    public const string SiteTemplatePreviewRenderFailed = "Site:090008";

    /// <summary>A preview of a page whose template is not in the theme, so a Razor view renders it; Data["Template"].</summary>
    public const string SiteThemePreviewPageNotScriban = "Site:090009";

    /// <summary>A publish with nothing to publish: the working copy is the live release.</summary>
    public const string SiteThemeNoChanges = "Site:090010";

    /// <summary>A default layout that is not a template path (it names a static file, or is not a valid path); Data["Layout"].</summary>
    public const string SiteThemeDefaultLayoutInvalid = "Site:090011";

    /// <summary>A default layout that names no template of the working copy; Data["Layout"].</summary>
    public const string SiteThemeDefaultLayoutNotFound = "Site:090012";

    /// <summary>
    /// A default layout that is a template of the working copy but not a layout: it writes no <c>body</c>, never calls
    /// <c>site_head</c>, or has front matter; Data["Layout"].
    /// </summary>
    public const string SiteThemeDefaultLayoutNotALayout = "Site:090013";

    /// <summary>A theme package that cannot be imported; Data["Problems"] lists what is wrong, one per line.</summary>
    public const string SiteThemePackageInvalid = "Site:090014";

    /// <summary>A theme download or upload link that has expired, has been used, or was not issued by this site.</summary>
    public const string SiteThemeTransferLinkInvalid = "Site:090015";

    // The site's languages (Site.EnabledLanguages), set through SiteLanguageManager.

    /// <summary>A list of languages with none in it: a site has to serve at least one.</summary>
    public const string SiteLanguagesRequired = "Site:100001";

    /// <summary>A language tag .NET does not know; Data["CultureName"].</summary>
    public const string SiteLanguageNotRecognized = "Site:100002";
}
