using Dignite.Site.Contents;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// Both neighbors in one response, since a detail page almost always renders both. Each side is null when
/// there is nothing in that direction - carried inside this DTO rather than as a null response body, which
/// an HTTP caller would otherwise receive as a bare 204.
/// </summary>
public class AdjacentContentsDto
{
    /// <summary>The next <i>older</i> content by <c>PublishTime</c>.</summary>
    public ContentDto? Previous { get; set; }

    /// <summary>The next <i>newer</i> content by <c>PublishTime</c>.</summary>
    public ContentDto? Next { get; set; }
}
