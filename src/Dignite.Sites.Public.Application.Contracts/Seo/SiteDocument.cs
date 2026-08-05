using System;

namespace Dignite.Sites.Public.Seo;

/// <summary>
/// A generated site document - a sitemap, <c>robots.txt</c>, or a feed - together with everything the
/// endpoint serving it needs.
/// <para>
/// A string rather than bytes on purpose: these are all UTF-8 text, encoding and any gzip belong to the
/// transport layer that knows the request's <c>Accept-Encoding</c>, and a string is what makes the
/// generated document directly assertable in a test.
/// </para>
/// </summary>
public class SiteDocument
{
    public SiteDocument(string contentType, string content, DateTime? lastModified = null)
    {
        ContentType = contentType;
        Content = content;
        LastModified = lastModified;
    }

    public string ContentType { get; }

    public string Content { get; }

    /// <summary>
    /// The newest real modification time behind this document, for a <c>Last-Modified</c> header. Never
    /// the generation time - the same rule the sitemap's own <c>lastmod</c> follows.
    /// </summary>
    public DateTime? LastModified { get; }
}
