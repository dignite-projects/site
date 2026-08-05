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
        var a = new ContentTypeField(FieldId, required: true, searchable: true, showInList: true, displayName: "Title", order: 1, schemaProperty: "headline");
        var b = new ContentTypeField(FieldId, required: true, searchable: true, showInList: true, displayName: "Title", order: 1, schemaProperty: "headline");

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    /// <summary>
    /// The regression this class exists to guard: two usages differing only in <c>SchemaProperty</c> must
    /// not compare equal, or a tenant mapping a field to a schema.org property and saving would silently
    /// fail to persist.
    /// </summary>
    [Fact]
    public void Should_Not_Be_Equal_When_Only_SchemaProperty_Differs()
    {
        var a = new ContentTypeField(FieldId, order: 0, schemaProperty: "headline");
        var b = new ContentTypeField(FieldId, order: 0, schemaProperty: "datePublished");

        a.ShouldNotBe(b);
        a.GetHashCode().ShouldNotBe(b.GetHashCode());
    }

    [Fact]
    public void Should_Not_Be_Equal_When_One_SchemaProperty_Is_Null()
    {
        var withMapping = new ContentTypeField(FieldId, order: 0, schemaProperty: "headline");
        var withoutMapping = new ContentTypeField(FieldId, order: 0, schemaProperty: null);

        withMapping.ShouldNotBe(withoutMapping);
    }

    [Fact]
    public void Should_Be_Equal_When_Both_SchemaProperty_Are_Null()
    {
        var a = new ContentTypeField(FieldId, order: 0);
        var b = new ContentTypeField(FieldId, order: 0);

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }
}
