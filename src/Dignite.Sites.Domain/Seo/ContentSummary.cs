namespace Dignite.Sites.Seo;

/// <summary>What a content is called and how it is described, once resolved.</summary>
public class ContentSummary
{
    public ContentSummary(string title, string? summary)
    {
        Title = title;
        Summary = summary;
    }

    /// <summary>Never blank - falls all the way back to the slug rather than leaving an item untitled.</summary>
    public string Title { get; }

    /// <summary>
    /// May be null. A feed item is valid with a title alone, and inventing a description from whatever
    /// field happened to be nearby would be worse than omitting it.
    /// </summary>
    public string? Summary { get; }
}
