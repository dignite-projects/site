namespace Dignite.Site.Public.Seo;

/// <summary>The share image a page's head carries, with its size when Site knows it (an image of the file library).</summary>
public record SiteShareImage(string Url, int? Width, int? Height);
