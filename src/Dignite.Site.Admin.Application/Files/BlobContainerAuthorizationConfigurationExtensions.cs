using System;
using Volo.Abp.BlobStoring;

namespace Dignite.Site.Admin.Files;

public static class BlobContainerAuthorizationConfigurationExtensions
{
    public static BlobContainerAuthorizationConfiguration GetAuthorizationConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new BlobContainerAuthorizationConfiguration(containerConfiguration);
    }

    public static void SetAuthorizationConfiguration(
        this BlobContainerConfiguration containerConfiguration,
        Action<BlobContainerAuthorizationConfiguration> configureAction)
    {
        configureAction(new BlobContainerAuthorizationConfiguration(containerConfiguration));
    }
}
