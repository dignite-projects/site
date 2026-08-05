using Volo.Abp.Identity;

namespace Dignite.Site.Host;

public static class HostConsts
{
    public const string AdminEmailDefaultValue = IdentityDataSeedContributor.AdminEmailDefaultValue;
    public const string AdminPasswordDefaultValue = "1q2w3E*";

    /// <summary>
    /// Matches the audience passed to <c>options.AddAudiences("Host")</c> in HostModule,
    /// and the "Host_App" client's "Host" scope in angular/src/environments/environment*.ts.
    /// </summary>
    public const string ApiScopeName = "Host";

    /// <summary>
    /// Matches clientId in angular/src/environments/environment*.ts.
    /// </summary>
    public const string AngularClientId = "Host_App";
}
