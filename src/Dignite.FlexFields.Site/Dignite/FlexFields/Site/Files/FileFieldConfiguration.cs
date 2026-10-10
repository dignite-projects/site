using Dignite.Abp.FlexFields;

namespace Dignite.FlexFields.Site.Files;

public class FileFieldConfiguration : FieldConfigurationBase
{
    /// <summary>
    /// The file container the picker browses and uploads to - one of Site's (<c>site-images</c>,
    /// <c>site-files</c>). No default: an unset value is left for the config editor to reject rather than
    /// silently substituted, see <see cref="FileFieldType"/>.
    /// </summary>
    public string? FileContainerName {
        get => ConfigurationDictionary.GetConfiguration<string?>(FileFieldConfigurationNames.FileContainerName, null);
        set => ConfigurationDictionary.SetConfiguration(FileFieldConfigurationNames.FileContainerName, value);
    }

    public bool UploadFileMultiple {
        get => ConfigurationDictionary.GetConfiguration(FileFieldConfigurationNames.UploadFileMultiple, false);
        set => ConfigurationDictionary.SetConfiguration(FileFieldConfigurationNames.UploadFileMultiple, value);
    }

    public FileFieldConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public FileFieldConfiguration() : base()
    {
    }
}
