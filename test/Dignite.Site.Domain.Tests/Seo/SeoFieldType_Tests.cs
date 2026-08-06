using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Dignite.Abp.FlexFields;
using Shouldly;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// <see cref="SeoFieldType"/> in isolation - no database, since <c>Validate</c>/<c>GetConfiguration</c>
/// only need the field type itself and a hand-built <see cref="FlexFieldValue"/>, the same way FlexFields'
/// own <c>FieldTypeValueShapes_Tests</c> exercises the built-in types.
/// </summary>
public class SeoFieldType_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly IFieldTypeResolver _resolver;
    private readonly SeoFieldType _fieldType;

    public SeoFieldType_Tests()
    {
        _resolver = GetRequiredService<IFieldTypeResolver>();
        _fieldType = GetRequiredService<SeoFieldType>();
    }

    [Fact]
    public void Should_Register_Under_Its_Well_Known_Name()
    {
        _resolver.Get(SeoFieldNames.FieldTypeName).ShouldBeOfType<SeoFieldType>();
    }

    /// <summary>
    /// The value is a composite object, not a scalar or list of scalars - there is no typed index column
    /// for it, the same reason FlexFields' own FileExplorer/RichText field types return null here.
    /// </summary>
    [Fact]
    public void Should_Not_Be_Indexable()
    {
        _fieldType.IndexValueType.ShouldBeNull();
    }

    [Fact]
    public void Should_Yield_No_Searchable_Values_Regardless_Of_Value()
    {
        var field = ToValue(new SeoFieldValue { NoIndex = true });

        _fieldType.GetSearchableValues(field).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Accept_A_Null_Value()
    {
        Validate(null).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Reject_A_Null_Value_When_Required()
    {
        Validate(null, required: true).ShouldNotBeEmpty();
    }

    [Fact]
    public void Should_Accept_A_Live_SeoFieldValue_When_Required()
    {
        Validate(new SeoFieldValue { MetaTitle = "Title" }, required: true).ShouldBeEmpty();
    }

    /// <summary>
    /// Presence, not completeness, is what <c>Required</c> checks - a value whose sub-properties are all
    /// blank still satisfies it, matching this type's own stance that nothing inside the bundle is
    /// individually required.
    /// </summary>
    [Fact]
    public void Should_Accept_An_Empty_SeoFieldValue_When_Required()
    {
        Validate(new SeoFieldValue(), required: true).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Accept_A_Live_SeoFieldValue()
    {
        Validate(new SeoFieldValue { MetaTitle = "Title", NoIndex = true }).ShouldBeEmpty();
    }

    /// <summary>The shape a value round-tripped through JSON storage arrives in.</summary>
    [Fact]
    public void Should_Accept_A_Json_Object_Shaped_Like_SeoFieldValue()
    {
        var element = JsonDocument.Parse("""{"metaTitle":"Title","noIndex":true}""").RootElement.Clone();

        Validate(element).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Accept_A_Json_Null()
    {
        var element = JsonDocument.Parse("null").RootElement.Clone();

        Validate(element).ShouldBeEmpty();
    }

    /// <summary>
    /// Not a shape this field type ever produces - most plausibly a value meant for a different field type
    /// stored under the wrong key, or a hand-crafted API call. Flagged rather than silently coerced.
    /// </summary>
    [Fact]
    public void Should_Reject_A_Bare_Json_String()
    {
        var element = JsonDocument.Parse("\"not-an-object\"").RootElement.Clone();

        Validate(element).ShouldNotBeEmpty();
    }

    [Fact]
    public void Should_Reject_A_Value_Of_An_Unrelated_Type()
    {
        Validate(42).ShouldNotBeEmpty();
    }

    [Fact]
    public void Configuration_Should_Default_To_The_Documented_Char_Limits()
    {
        var configuration = (SeoConfiguration)_fieldType.GetConfiguration(new FieldConfigurationDictionary());

        configuration.MetaTitleCharLimit.ShouldBe(60);
        configuration.MetaDescriptionCharLimit.ShouldBe(160);
    }

    [Fact]
    public void Configuration_Should_Round_Trip_Custom_Char_Limits()
    {
        var dictionary = new FieldConfigurationDictionary();
        var written = new SeoConfiguration(dictionary) { MetaTitleCharLimit = 70, MetaDescriptionCharLimit = 180 };

        var read = (SeoConfiguration)_fieldType.GetConfiguration(written.ConfigurationDictionary);

        read.MetaTitleCharLimit.ShouldBe(70);
        read.MetaDescriptionCharLimit.ShouldBe(180);
    }

    private IReadOnlyList<ValidationResult> Validate(object? value, bool required = false)
    {
        return _fieldType.Validate(new FieldValidationArgs(ToValue(value, required)));
    }

    private static FlexFieldValue ToValue(object? value, bool required = false)
    {
        var data = new FlexFieldData(
            Guid.NewGuid(), SeoFieldNames.FieldName, "SEO", SeoFieldNames.FieldTypeName);

        return new FlexFieldValue(data, required: required, searchable: true, value: value);
    }
}
