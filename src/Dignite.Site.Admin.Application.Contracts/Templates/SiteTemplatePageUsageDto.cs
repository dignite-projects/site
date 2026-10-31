using System;

namespace Dignite.Site.Admin.Templates;

/// <summary>One page a template takes part in rendering - see <see cref="SiteTemplateUsageDto.Pages"/>.</summary>
public class SiteTemplatePageUsageDto
{
    public Guid PageId { get; set; }

    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string Route { get; set; } = default!;

    /// <summary>The page's own <c>Template</c> - for a layout, partial or asset, another template's name.</summary>
    public string Template { get; set; } = default!;

    /// <summary>Whether the page is the site's home page (<c>Page.IsHomeRoute</c>).</summary>
    public bool IsHomePage { get; set; }

    /// <summary>Whether the page's route carries a slug - whether contents have addresses of their own beneath it.</summary>
    public bool HasSlug { get; set; }

    /// <summary>The page's own address in the default language, without a language prefix: <c>/blog</c> for <c>/blog/{slug?}</c>.</summary>
    public string Path { get; set; } = default!;

    /// <summary>
    /// A content to preview the page's detail view with, when <see cref="HasSlug"/>: the newest publicly
    /// visible one with a slug, in a language the site serves. Null when there is none, or the page has no slug.
    /// </summary>
    public SiteTemplateSampleContentDto? SampleContent { get; set; }
}
