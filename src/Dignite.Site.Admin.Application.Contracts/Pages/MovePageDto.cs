using System;

namespace Dignite.Site.Admin.Pages;

/// <summary>
/// What a drag-and-drop in the Admin UI's page list sends - lighter than <see cref="UpdatePageDto"/>,
/// since a drag only ever changes where a page sits in the tree.
/// </summary>
public class MovePageDto
{
    /// <summary>Null for a top-level page.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// The target index among the page's new siblings - not a literal value to store. The destination
    /// sibling group is densely renumbered to <c>0..N</c> with the page spliced in here.
    /// </summary>
    public int Order { get; set; }
}
