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

    public const string PrimaryDomainNotConfigured = "Site:050001";

    /// <summary>
    /// Shared across every "shaped identifier" property (Page.Name/Route/Template, ContentType.Name,
    /// Field.Name, Content.Slug) rather than one code per property - the recovery is always the same
    /// (fix the value's characters), so the reason does not need its own number per field.
    /// </summary>
    public const string InvalidValueFormat = "Site:060001";
}
