namespace Dignite.Sites;

/// <summary>
/// Error codes for the module's business exceptions. Each one is looked up in the <c>Sites</c>
/// localization resource by this exact string - <c>SitesDomainSharedModule</c> maps the <c>Sites</c>
/// namespace to <c>SitesResource</c> - so a code added here needs a matching entry in
/// <c>Localization/Sites/*.json</c> or the raw code surfaces to the caller.
/// </summary>
public static class SitesErrorCodes
{
    public const string PageRouteAlreadyExists = "Sites:010001";
    public const string PageNameAlreadyExists = "Sites:010002";
    public const string InvalidContentPathPattern = "Sites:010003";

    public const string ContentTypeNameAlreadyExists = "Sites:020001";
    public const string ContentTypeFieldDuplicated = "Sites:020002";

    public const string FieldNameAlreadyExists = "Sites:030001";

    public const string ContentSlugAlreadyExists = "Sites:040001";
    public const string ContentPageInconsistent = "Sites:040002";
}
