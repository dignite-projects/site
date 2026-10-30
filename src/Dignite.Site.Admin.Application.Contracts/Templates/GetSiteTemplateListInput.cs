namespace Dignite.Site.Admin.Templates;

public class GetSiteTemplateListInput
{
    public string? Filter { get; set; }

    /// <summary>Only templates whose name starts with this, e.g. <c>partials/</c> or <c>assets/</c>.</summary>
    public string? Prefix { get; set; }
}
