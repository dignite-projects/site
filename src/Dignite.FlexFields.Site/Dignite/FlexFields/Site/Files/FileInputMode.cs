namespace Dignite.FlexFields.Site.Files;

/// <summary>
/// How the admin UI attaches files to a file field. The numbers are persisted in the field's configuration
/// (<see cref="FileFieldConfigurationNames.InputMode"/>).
/// </summary>
public enum FileInputMode
{
    /// <summary>The user chooses files on their computer and they are uploaded at once.</summary>
    Upload = 0,

    /// <summary>
    /// A window shows the files already uploaded to the container - its directories too, when the container has
    /// them - and the user picks from them, or uploads into the directory shown.
    /// </summary>
    Library = 1
}
