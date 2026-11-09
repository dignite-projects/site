using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Site.Files;

/// <summary>
/// How a container stores the pictures it is given, kept in its <see cref="BlobContainerConfiguration"/>. Set by the
/// administration module together with the contributor that does the work, and read by
/// <see cref="FileDescriptorManager"/>, which names the blob and records its type before the save: so the two agree
/// on what is stored without the domain knowing the contributor.
/// </summary>
public class SiteImageStorageConfiguration
{
    public const string StoreJpegAsWebpName = "Dignite.Site.ImageStorage.StoreJpegAsWebp";
    public const string WebpQualityName = "Dignite.Site.ImageStorage.WebpQuality";

    protected BlobContainerConfiguration ContainerConfiguration { get; }

    public SiteImageStorageConfiguration(BlobContainerConfiguration containerConfiguration)
    {
        ContainerConfiguration = Check.NotNull(containerConfiguration, nameof(containerConfiguration));
    }

    /// <summary>
    /// Whether a JPEG is stored as lossy WebP: its blob name ends in <c>.webp</c> and its recorded type is
    /// <c>image/webp</c>. Every JPEG is converted, whatever the size of the result, so the name and the type can be
    /// decided before the save.
    /// </summary>
    public bool StoreJpegAsWebp
    {
        get => ContainerConfiguration.GetConfigurationOrDefault(StoreJpegAsWebpName, false);
        set => ContainerConfiguration.SetConfiguration(StoreJpegAsWebpName, value);
    }

    /// <summary>The quality, 0-100, of the WebP a JPEG is stored as.</summary>
    public int WebpQuality
    {
        get => ContainerConfiguration.GetConfigurationOrDefault(WebpQualityName, 85);
        set => ContainerConfiguration.SetConfiguration(WebpQualityName, value);
    }

    /// <summary>
    /// The type a picture detected as <paramref name="mimeType"/> is stored as in this container.
    /// </summary>
    public virtual string GetStoredMimeType(string mimeType)
    {
        return StoreJpegAsWebp && mimeType == "image/jpeg" ? "image/webp" : mimeType;
    }

    public virtual void Validate()
    {
        if (WebpQuality is < 1 or > 100)
        {
            throw new AbpException("The WebP quality of a container must be between 1 and 100.");
        }
    }
}

public static class SiteImageStorageConfigurationExtensions
{
    public static SiteImageStorageConfiguration GetSiteImageStorageConfiguration(this BlobContainerConfiguration containerConfiguration)
    {
        return new SiteImageStorageConfiguration(containerConfiguration);
    }
}
