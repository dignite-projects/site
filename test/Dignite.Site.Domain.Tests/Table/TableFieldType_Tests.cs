using System.Text.Json;
using Dignite.FlexFields.Site.Table;
using Shouldly;
using Xunit;

namespace Dignite.Site.Table;

/// <summary>
/// <see cref="TableFieldType.Normalize"/> in isolation - see <c>MatrixFieldType_Tests</c>, whose
/// reasoning this mirrors exactly (<c>TableRow</c> has only <c>Values</c>, no type tag).
/// </summary>
public class TableFieldType_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly TableFieldType _fieldType;

    public TableFieldType_Tests()
    {
        _fieldType = GetRequiredService<TableFieldType>();
    }

    [Fact]
    public void Should_Normalize_The_Row_Wrappers_Casing_To_CamelCase()
    {
        var element = JsonDocument.Parse("""[{"Values":{"text":"Hello"}}]""").RootElement.Clone();

        var normalized = (JsonElement)_fieldType.Normalize(element)!;

        normalized[0].GetProperty("values").GetProperty("text").GetString().ShouldBe("Hello");
    }

    [Fact]
    public void Should_Leave_Null_Unchanged_When_Normalizing()
    {
        _fieldType.Normalize(null).ShouldBeNull();
    }

    [Fact]
    public void Should_Normalize_A_Non_Array_Value_To_An_Empty_Array()
    {
        var element = JsonDocument.Parse("\"not-an-array\"").RootElement.Clone();

        var normalized = (JsonElement)_fieldType.Normalize(element)!;

        normalized.ValueKind.ShouldBe(JsonValueKind.Array);
        normalized.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void Should_Leave_A_Value_With_A_Malformed_Element_Unchanged_When_Normalizing()
    {
        var element = JsonDocument.Parse("""[{"Values":{}},"not-a-row"]""").RootElement.Clone();

        var normalized = _fieldType.Normalize(element);

        ((JsonElement)normalized!).GetRawText().ShouldBe(element.GetRawText());
    }
}
