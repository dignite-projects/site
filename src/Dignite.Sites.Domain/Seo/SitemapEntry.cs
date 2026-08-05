using System;

namespace Dignite.Sites.Seo;

/// <summary>
/// One URL bound for the sitemap: where it is, when it genuinely last changed, and how it ranks against
/// the site's other URLs (总体设计 §5.2).
/// </summary>
public class SitemapEntry
{
    public SitemapEntry(string location, DateTime lastModified, double priority)
    {
        Location = location;
        LastModified = lastModified;
        Priority = priority;
    }

    /// <summary>The absolute, percent-encoded URL.</summary>
    public string Location { get; }

    /// <summary>
    /// <b>Never the generation time.</b> Derived only from stored columns, so two runs of the generator
    /// produce the same value - see <see cref="SitemapEntrySource"/> for why that matters so much.
    /// </summary>
    public DateTime LastModified { get; private set; }

    public double Priority { get; private set; }

    /// <summary>
    /// Folds a second claim on the same URL into this one. Two claims are normal rather than exceptional:
    /// a page carrying a single empty-slug content is reached by the page's own route <i>and</i> by that
    /// content's path, and they are the same URL. Taking the maxima makes the result independent of which
    /// of the two the generator happened to visit first.
    /// </summary>
    public void Merge(SitemapEntry other)
    {
        if (other.LastModified > LastModified)
        {
            LastModified = other.LastModified;
        }

        if (other.Priority > Priority)
        {
            Priority = other.Priority;
        }
    }
}
