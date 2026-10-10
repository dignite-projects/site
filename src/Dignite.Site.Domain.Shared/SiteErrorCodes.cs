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

    // The file library's directories (Dignite.Site.Directories). A directory belongs to the user who
    // created it, so "not found" also answers for another user's or another tenant's directory.
    public const string DirectoryNameAlreadyExists = "Site:070001";
    public const string DirectoryNotFound = "Site:070002";
    public const string DirectoryCannotMoveAcrossContainers = "Site:070003";
    public const string DirectoryCannotMoveIntoItself = "Site:070004";
    public const string DirectoryHasChildren = "Site:070005";
    public const string DirectoryHasFiles = "Site:070006";
    public const string DirectoryRequiresUser = "Site:070007";

    // The file library's files (Dignite.Site.Files). Upload rules - size, type, image bounds - are
    // Dignite.Abp.FileStoring's own codes, raised by IFileStorer.
    public const string FileContainerNotAvailable = "Site:080001";
    public const string FileSortingNotSupported = "Site:080002";
}
