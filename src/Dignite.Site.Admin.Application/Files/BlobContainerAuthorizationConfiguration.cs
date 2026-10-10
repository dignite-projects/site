using Volo.Abp.BlobStoring;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Which permission each file-library operation needs in one blob container, stored on the container's
/// own <see cref="BlobContainerConfiguration"/> (<see cref="BlobContainerAuthorizationConfigurationExtensions.SetAuthorizationConfiguration"/>).
/// <c>SiteAdminApplicationModule</c> sets it for each of <c>SiteFileContainerNames</c>.
/// </summary>
public class BlobContainerAuthorizationConfiguration
{
    private readonly BlobContainerConfiguration _containerConfiguration;

    public BlobContainerAuthorizationConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        _containerConfiguration = containerConfiguration;
    }

    private void Set(string name, string? value)
    {
        // BlobContainerConfiguration refuses a null value; null here means "unset", i.e. the default rule.
        if (value == null)
        {
            _containerConfiguration.ClearConfiguration(name);
        }
        else
        {
            _containerConfiguration.SetConfiguration(name, value);
        }
    }

    /// <summary>Unset: nobody may create a directory.</summary>
    public string? CreateDirectoryPermissionName {
        get => _containerConfiguration.GetConfigurationOrDefault<string?>(BlobContainerAuthorizationConfigurationNames.CreateDirectoryPermission);
        set => Set(BlobContainerAuthorizationConfigurationNames.CreateDirectoryPermission, value);
    }

    /// <summary>Unset: only a user with <c>SiteAdmin.Contents</c> may upload.</summary>
    public string? CreateFilePermissionName {
        get => _containerConfiguration.GetConfigurationOrDefault<string?>(BlobContainerAuthorizationConfigurationNames.CreateFilePermission);
        set => Set(BlobContainerAuthorizationConfigurationNames.CreateFilePermission, value);
    }

    /// <summary>Unset: only the uploader (or a user with <c>SiteAdmin.Contents</c>) may rename or move a file.</summary>
    public string? UpdateFilePermissionName {
        get => _containerConfiguration.GetConfigurationOrDefault<string?>(BlobContainerAuthorizationConfigurationNames.UpdateFilePermission);
        set => Set(BlobContainerAuthorizationConfigurationNames.UpdateFilePermission, value);
    }

    /// <summary>Unset: only the uploader (or a user with <c>SiteAdmin.Contents</c>) may delete a file.</summary>
    public string? DeleteFilePermissionName {
        get => _containerConfiguration.GetConfigurationOrDefault<string?>(BlobContainerAuthorizationConfigurationNames.DeleteFilePermission);
        set => Set(BlobContainerAuthorizationConfigurationNames.DeleteFilePermission, value);
    }

    /// <summary>
    /// Unset: anyone may read a file's descriptor. Site leaves it unset for its containers - their files are
    /// served to anonymous visitors by the public read endpoint anyway.
    /// </summary>
    public string? GetFilePermissionName {
        get => _containerConfiguration.GetConfigurationOrDefault<string?>(BlobContainerAuthorizationConfigurationNames.GetFilePermission);
        set => Set(BlobContainerAuthorizationConfigurationNames.GetFilePermission, value);
    }
}
