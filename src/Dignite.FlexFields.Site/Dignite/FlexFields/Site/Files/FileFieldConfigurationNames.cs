namespace Dignite.FlexFields.Site.Files;

/// <summary>
/// The configuration keys of <see cref="FileFieldType"/>. <b>Persisted data:</b> they are the keys stored in
/// every field's (and every Matrix block's nested) configuration since the field type lived in
/// <c>Dignite.Abp.FlexFields</c>, so they keep that module's names - the same rule as the registration key
/// <see cref="FileFieldType.ControlName"/>. Renaming one is a data migration, not a refactoring.
/// </summary>
public static class FileFieldConfigurationNames
{
    public const string FileContainerName = "FileExplorer.FileContainerName";
    public const string UploadFileMultiple = "FileExplorer.UploadFileMultiple";
}
