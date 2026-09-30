using System;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// The page and language are never inputs - both come from the content the neighbors are asked for,
/// since previous/next across languages or pages means nothing to a reader.
/// </summary>
public class GetAdjacentContentsInput
{
    /// <summary>
    /// Optionally keeps the neighbors to one content type. Left empty, they span the whole page, the same
    /// scope <c>content-list</c> lists by default.
    /// </summary>
    public Guid? ContentTypeId { get; set; }
}
