using System;

namespace Dignite.Site.Admin.Templates;

/// <summary>A content whose address previews a page's detail view - see <see cref="SiteTemplatePageUsageDto.SampleContent"/>.</summary>
public class SiteTemplateSampleContentDto
{
    public Guid ContentId { get; set; }

    public string Slug { get; set; } = default!;

    public string CultureName { get; set; } = default!;

    /// <summary>
    /// The content's site-relative address, language prefix included (<c>/blog/my-trip</c>,
    /// <c>/zh-Hans/blog/my-trip</c>) - the same path <c>ContentDto.Url</c> carries.
    /// </summary>
    public string Path { get; set; } = default!;
}
