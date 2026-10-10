using System;

namespace Dignite.Site.Admin.Directories;

public class MoveDirectoryInput
{
    public MoveDirectoryInput()
    {
    }

    public MoveDirectoryInput(Guid? parentId, int order)
    {
        ParentId = parentId;
        Order = order;
    }

    /// <summary>The new parent, or <c>null</c> for the top level.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>The position among the new siblings; those at or after it shift down by one.</summary>
    public int Order { get; set; }
}
