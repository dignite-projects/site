using System;
using Shouldly;
using Xunit;

namespace Dignite.Site.ContentTypes;

/// <summary>
/// <see cref="ContentTypeField"/>'s value equality, in isolation - no database. Its own doc comment
/// explains why this matters: the field list is persisted through a value converter, and
/// <c>ContentTypeFieldListValueComparer</c> (EF Core layer) delegates entirely to this class's
/// <c>Equals</c>/<c>GetHashCode</c>. A property that is not wired into both is invisible to change
/// tracking - the edit is accepted, nothing errors, and the old value is still there on reload.
/// </summary>
public class ContentTypeField_Tests
{
    private static readonly Guid FieldId = Guid.NewGuid();

    [Fact]
    public void Should_Be_Equal_When_Every_Property_Matches()
    {
        var a = new ContentTypeField(FieldId, required: true, searchable: true, showInList: true, displayName: "Title", order: 1);
        var b = new ContentTypeField(FieldId, required: true, searchable: true, showInList: true, displayName: "Title", order: 1);

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }
}
