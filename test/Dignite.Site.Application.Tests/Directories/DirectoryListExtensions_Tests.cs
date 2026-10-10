using System;
using Shouldly;
using Xunit;

namespace Dignite.Site.Admin.Directories;

public class DirectoryListExtensions_Tests
{
    [Fact]
    public void BuildTree_Should_Nest_Children_Under_Their_Parents()
    {
        var root = new DirectoryDescriptorInfoDto { Id = Guid.NewGuid() };
        var child = new DirectoryDescriptorInfoDto { Id = Guid.NewGuid(), ParentId = root.Id };
        var grandchild = new DirectoryDescriptorInfoDto { Id = Guid.NewGuid(), ParentId = child.Id };

        var tree = new[] { root, child, grandchild }.BuildTree();

        tree.ShouldHaveSingleItem().Id.ShouldBe(root.Id);
        tree[0].Children.ShouldHaveSingleItem().Children.ShouldHaveSingleItem().Id.ShouldBe(grandchild.Id);
        grandchild.GetParentList(tree).ShouldBe(new[] { root, child });
    }

    /// <summary>A cycle already in the stored data must end the walk, not recurse forever.</summary>
    [Fact]
    public void BuildTree_Should_Stop_When_The_Source_Contains_A_Cycle()
    {
        var first = new DirectoryDescriptorInfoDto { Id = Guid.NewGuid() };
        var second = new DirectoryDescriptorInfoDto { Id = Guid.NewGuid(), ParentId = first.Id };
        first.ParentId = second.Id;

        var tree = new[] { first, second }.BuildTree();

        tree.Count.ShouldBe(1);
        tree[0].Children.Count.ShouldBe(1);
        tree.ToLevelList().Count.ShouldBe(2);
    }
}
