using System.Text.Json;
using Dignite.FlexFields.Site.Matrix;
using Shouldly;
using Xunit;

namespace Dignite.Site.Matrix;

/// <summary>
/// <see cref="MatrixFieldType.Normalize"/> in isolation - the outer block-array wrapper's own casing, not
/// <c>Validate</c> (already covered elsewhere) or the admin-configured sub-field names inside
/// <c>MatrixBlockValue.Values</c>, which are a free-form bag <c>Normalize</c> deliberately leaves alone.
/// </summary>
public class MatrixFieldType_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly MatrixFieldType _fieldType;

    public MatrixFieldType_Tests()
    {
        _fieldType = GetRequiredService<MatrixFieldType>();
    }

    /// <summary>
    /// The same regression Seo closes, one level up: <c>MatrixBlockValue</c>'s own fixed properties
    /// (<c>BlockTypeName</c>, <c>Values</c>) must round-trip to camelCase regardless of how a caller cased
    /// them, even though the keys inside <c>Values</c> - the admin's own sub-field names - are untouched.
    /// </summary>
    [Fact]
    public void Should_Normalize_The_Block_Wrappers_Casing_To_CamelCase()
    {
        var element = JsonDocument.Parse(
            """[{"BlockTypeName":"quote","Values":{"text":"Hello"}}]""").RootElement.Clone();

        var normalized = (JsonElement)_fieldType.Normalize(element)!;

        normalized[0].GetProperty("blockTypeName").GetString().ShouldBe("quote");
        normalized[0].GetProperty("values").GetProperty("text").GetString().ShouldBe("Hello");
    }

    [Fact]
    public void Should_Leave_Null_Unchanged_When_Normalizing()
    {
        _fieldType.Normalize(null).ShouldBeNull();
    }

    /// <summary>
    /// A value that is not array-shaped at all normalizes to <c>[]</c>, same as <c>ReadBlocks</c> already
    /// resolves it for <c>Validate</c> today (the <c>default</c> case) - this is not a behavior change,
    /// just the same coalescing applied one step earlier.
    /// </summary>
    [Fact]
    public void Should_Normalize_A_Non_Array_Value_To_An_Empty_Array()
    {
        var element = JsonDocument.Parse("\"not-an-array\"").RootElement.Clone();

        var normalized = (JsonElement)_fieldType.Normalize(element)!;

        normalized.ValueKind.ShouldBe(JsonValueKind.Array);
        normalized.GetArrayLength().ShouldBe(0);
    }

    /// <summary>
    /// Unlike a non-array value, an array with a structurally broken <i>element</i> (a bare string where a
    /// block object belongs) makes <c>System.Text.Json</c> throw while deserializing - today, that surfaces
    /// from <c>Validate</c>'s own call to <c>ReadBlocks</c>. This method must not turn that into a new,
    /// earlier failure point: it catches the same exception and returns the value unchanged, so the
    /// existing (unrelated, pre-existing) crash still happens at the same place it always did.
    /// </summary>
    [Fact]
    public void Should_Leave_A_Value_With_A_Malformed_Element_Unchanged_When_Normalizing()
    {
        var element = JsonDocument.Parse(
            """[{"BlockTypeName":"quote","Values":{}},"not-a-block"]""").RootElement.Clone();

        var normalized = _fieldType.Normalize(element);

        ((JsonElement)normalized!).GetRawText().ShouldBe(element.GetRawText());
    }
}
