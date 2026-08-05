using Dignite.Abp.FlexFields;

namespace Dignite.Sites.Seo;

public static class SeoConfigurationNames
{
    public const string MetaTitleCharLimit = "Seo.MetaTitleCharLimit";
    public const string MetaDescriptionCharLimit = "Seo.MetaDescriptionCharLimit";
}

/// <summary>
/// Typed wrapper over <see cref="SeoFieldType"/>'s configuration (mirrors <c>TextConfiguration</c>).
/// <para>
/// Both limits are advisory, not enforced - <see cref="SeoFieldType.Validate"/> does not reject a longer
/// value, because a search engine truncates an over-length title or description rather than rejecting it.
/// They exist as a reference threshold for an editing UI or AI-generated content to aim for.
/// </para>
/// </summary>
public class SeoConfiguration : FieldConfigurationBase
{
    /// <summary>Default value: 60 - Google's practical truncation point for a search result title.</summary>
    public int MetaTitleCharLimit
    {
        get => ConfigurationDictionary.GetConfiguration(SeoConfigurationNames.MetaTitleCharLimit, 60);
        set => ConfigurationDictionary.SetConfiguration(SeoConfigurationNames.MetaTitleCharLimit, value);
    }

    /// <summary>Default value: 160 - the conventional SERP snippet length.</summary>
    public int MetaDescriptionCharLimit
    {
        get => ConfigurationDictionary.GetConfiguration(SeoConfigurationNames.MetaDescriptionCharLimit, 160);
        set => ConfigurationDictionary.SetConfiguration(SeoConfigurationNames.MetaDescriptionCharLimit, value);
    }

    public SeoConfiguration() : base()
    {
    }

    public SeoConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }
}
