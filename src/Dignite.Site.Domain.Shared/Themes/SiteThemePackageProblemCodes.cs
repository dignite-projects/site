namespace Dignite.Site.Themes;

/// <summary>
/// What can be wrong with a theme package (<c>Site:090014</c>). As with <see cref="SiteThemeIssueCodes"/>, each code is
/// also the key of its message in the <c>Site</c> localization resource; the message's <c>{Entry}</c> is the zip entry
/// the problem is in and <c>{Value}</c> the limit or extension it is about.
/// </summary>
public static class SiteThemePackageProblemCodes
{
    private const string Prefix = "ThemePackage:";

    /// <summary>The file is not a zip.</summary>
    public const string NotZip = Prefix + "NotZip";

    /// <summary>The package is more than {Value} bytes as uploaded.</summary>
    public const string PackageTooLarge = Prefix + "PackageTooLarge";

    /// <summary>The files are more than {Value} bytes unpacked.</summary>
    public const string FilesTooLarge = Prefix + "FilesTooLarge";

    /// <summary>The package has more than {Value} files.</summary>
    public const string TooManyFiles = Prefix + "TooManyFiles";

    /// <summary>{Entry} reaches outside the theme: <c>..</c>, a leading <c>/</c> or a backslash.</summary>
    public const string OutsideTheme = Prefix + "OutsideTheme";

    /// <summary>{Entry} is not of a theme file path's shape.</summary>
    public const string InvalidPath = Prefix + "InvalidPath";

    /// <summary>{Entry} has the extension {Value}, which no theme file may have.</summary>
    public const string ExtensionNotAllowed = Prefix + "ExtensionNotAllowed";

    /// <summary>{Entry} is in the package twice.</summary>
    public const string DuplicatePath = Prefix + "DuplicatePath";

    /// <summary>{Entry} holds more than its size in the zip says.</summary>
    public const string SizeMismatch = Prefix + "SizeMismatch";

    /// <summary>{Entry} is not UTF-8 text.</summary>
    public const string NotUtf8 = Prefix + "NotUtf8";

    /// <summary>{Entry} is longer than the {Value} characters a theme file may hold.</summary>
    public const string FileTooLong = Prefix + "FileTooLong";

    /// <summary>The manifest ({Entry}) is not valid JSON.</summary>
    public const string ManifestNotJson = Prefix + "ManifestNotJson";

    /// <summary>The manifest ({Entry}) is JSON but not an object.</summary>
    public const string ManifestNotObject = Prefix + "ManifestNotObject";

    /// <summary>The manifest's ({Entry}) defaultLayout is neither a path nor null.</summary>
    public const string ManifestDefaultLayout = Prefix + "ManifestDefaultLayout";
}
