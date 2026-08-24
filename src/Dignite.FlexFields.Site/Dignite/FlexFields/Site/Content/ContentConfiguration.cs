using System;
using Dignite.Abp.FlexFields;

namespace Dignite.FlexFields.Site.Content;

public class ContentConfiguration : FieldConfigurationBase
{
    /// <summary>
    /// Restricts the picker to Content of this content type. Left as a bare <see cref="Guid"/> rather
    /// than a reference to Site's own <c>ContentType</c> - resolving it is the picker/renderer's job,
    /// not this field type's (mirrors DynamicForms' own <c>EntryConfiguration.SectionId</c>).
    /// </summary>
    public Guid? ContentTypeId {
        get => ConfigurationDictionary.GetConfiguration<Guid?>(ContentConfigurationNames.ContentTypeId, null);
        set => ConfigurationDictionary.SetConfiguration(ContentConfigurationNames.ContentTypeId, value);
    }

    public bool Multiple {
        get => ConfigurationDictionary.GetConfiguration(ContentConfigurationNames.Multiple, false);
        set => ConfigurationDictionary.SetConfiguration(ContentConfigurationNames.Multiple, value);
    }

    public string? Placeholder {
        get => ConfigurationDictionary.GetConfiguration<string?>(ContentConfigurationNames.Placeholder, null);
        set => ConfigurationDictionary.SetConfiguration(ContentConfigurationNames.Placeholder, value);
    }

    public ContentConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public ContentConfiguration() : base()
    {
    }
}
