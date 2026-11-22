using System.Linq;
using Dignite.FlexFields.Site.Files;
using Shouldly;
using Xunit;

namespace Dignite.Site.Contents;

/// <summary>
/// How a file field attaches files - uploaded at once, or picked from the files already uploaded - is a configuration
/// key of the field type, stored as the number of <see cref="FileInputMode"/>, as the date field's input mode is.
/// </summary>
public class FileFieldInputMode_Tests
{
    [Fact]
    public void The_Field_Type_Declares_The_Mode_As_An_Enum_That_Defaults_To_Upload()
    {
        var key = new FileFieldType().ConfigurationKeys!.Single(k => k.Name == FileFieldConfigurationNames.InputMode);

        key.Name.ShouldBe("FileExplorer.InputMode");
        key.ValueType.ShouldBe(typeof(FileInputMode));
        key.DefaultValue.ShouldBe((int)FileInputMode.Upload);
        key.EnumMembers!.Count.ShouldBe(2);
        key.Required.ShouldBeFalse();
    }

    [Fact]
    public void A_Field_Without_The_Key_Uploads()
    {
        new FileFieldConfiguration().InputMode.ShouldBe(FileInputMode.Upload);
    }

    [Fact]
    public void The_Mode_Round_Trips_Through_The_Configuration()
    {
        var configuration = new FileFieldConfiguration { FileContainerName = "images", InputMode = FileInputMode.Library };

        new FileFieldConfiguration(configuration.ConfigurationDictionary).InputMode.ShouldBe(FileInputMode.Library);
        new FileFieldConfiguration(configuration.ConfigurationDictionary).FileContainerName.ShouldBe("images");
    }
}
