using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Dignite.Site.Admin.Directories;

/// <summary>
/// Tree helpers over a flat directory list. Every walk tracks the ids it has visited, so a cycle already in
/// the stored data (a parent pointing at its own descendant) ends a walk instead of recursing forever.
/// </summary>
public static class DirectoryListExtensions
{
    public static DirectoryDescriptorInfoDto? FindById([NotNull] this IEnumerable<DirectoryDescriptorInfoDto> source, Guid id)
    {
        return FindById(source, id, new HashSet<Guid>());
    }

    /// <summary>Flattens a tree depth-first, each node followed by its descendants.</summary>
    public static IReadOnlyList<DirectoryDescriptorInfoDto> ToLevelList([NotNull] this IReadOnlyList<DirectoryDescriptorInfoDto> source)
    {
        var result = new List<DirectoryDescriptorInfoDto>();
        var visited = new HashSet<Guid>();
        foreach (var directory in source)
        {
            if (!visited.Add(directory.Id))
            {
                continue;
            }

            result.Add(directory);
            FindChildren(result, directory, visited);
        }

        return result;
    }

    /// <summary>
    /// Builds the tree from a flat list ordered by parent (<c>GetAllByUserAsync</c>'s order): the nodes
    /// sharing the first item's parent are the roots.
    /// </summary>
    public static IReadOnlyList<DirectoryDescriptorInfoDto> BuildTree([NotNull] this IReadOnlyList<DirectoryDescriptorInfoDto> source)
    {
        if (!source.Any())
        {
            return source;
        }

        var parentId = source.First().ParentId;
        var tree = source.Where(d => d.ParentId == parentId).ToList();
        foreach (var directory in tree)
        {
            AddChildren(directory, source, new HashSet<Guid> { directory.Id });
        }

        return tree;
    }

    /// <summary>The ancestors of <paramref name="directory"/>, outermost first.</summary>
    public static IReadOnlyList<DirectoryDescriptorInfoDto> GetParentList([NotNull] this DirectoryDescriptorInfoDto directory, IEnumerable<DirectoryDescriptorInfoDto> source)
    {
        var result = new List<DirectoryDescriptorInfoDto>();
        FindParent(directory, source, result, new HashSet<Guid> { directory.Id });
        result.Reverse();
        return result;
    }

    private static DirectoryDescriptorInfoDto? FindById(IEnumerable<DirectoryDescriptorInfoDto> source, Guid id, HashSet<Guid> visited)
    {
        foreach (var item in source)
        {
            if (!visited.Add(item.Id))
            {
                continue;
            }

            if (item.Id == id)
            {
                return item;
            }

            if (item.Children.Any())
            {
                var result = FindById(item.Children, id, visited);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static void FindChildren(List<DirectoryDescriptorInfoDto> list, DirectoryDescriptorInfoDto directory, HashSet<Guid> visited)
    {
        foreach (var child in directory.Children)
        {
            if (!visited.Add(child.Id))
            {
                continue;
            }

            list.Add(child);
            FindChildren(list, child, visited);
        }
    }

    private static void AddChildren(DirectoryDescriptorInfoDto parent, IReadOnlyList<DirectoryDescriptorInfoDto> list, HashSet<Guid> visited)
    {
        foreach (var child in list.Where(d => d.ParentId == parent.Id).ToList())
        {
            if (!visited.Add(child.Id))
            {
                continue;
            }

            parent.AddChild(child);
            AddChildren(child, list, visited);
        }
    }

    private static void FindParent(DirectoryDescriptorInfoDto directory, IEnumerable<DirectoryDescriptorInfoDto> source, List<DirectoryDescriptorInfoDto> result, HashSet<Guid> visited)
    {
        if (!directory.ParentId.HasValue)
        {
            return;
        }

        var parent = source.FindById(directory.ParentId.Value);
        if (parent != null && visited.Add(parent.Id))
        {
            result.Add(parent);
            FindParent(parent, source, result, visited);
        }
    }
}
