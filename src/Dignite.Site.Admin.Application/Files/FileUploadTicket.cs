using System;
using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.Security.Claims;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// What an upload link says, signed inside it (<see cref="FileUploadTicketProtector"/>): who it was issued to, for
/// which container, name and directory, how much it takes and until when.
/// <para>
/// The link is used by someone with no credential of their own - a command line that was handed the link - so the
/// ticket carries the identity the upload runs as: the user, tenant, roles and client of the call that asked for it.
/// Redeeming it checks the permissions of that identity again, at the time of the upload, so a role changed in the
/// few minutes between does not leave the link more powerful than its issuer.
/// </para>
/// </summary>
public sealed record FileUploadTicket(
    int Version,
    string Nonce,
    DateTimeOffset ExpiresAt,
    Guid? TenantId,
    Guid? UserId,
    string? UserName,
    string[] Roles,
    string? ClientId,
    string ContainerName,
    string FileName,
    Guid? DirectoryId,
    long MaxBytes)
{
    public const int CurrentVersion = 1;

    /// <summary>The authentication type of <see cref="ToPrincipal"/>, so a log can tell an upload link's identity from a sign-in.</summary>
    public const string AuthenticationType = "SiteUploadTicket";

    /// <summary>The identity the upload runs as: the claims the permission checks read, nothing else.</summary>
    public ClaimsPrincipal ToPrincipal()
    {
        var claims = new List<Claim>();

        if (UserId.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.UserId, UserId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(UserName))
        {
            claims.Add(new Claim(AbpClaimTypes.UserName, UserName));
        }

        if (TenantId.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.TenantId, TenantId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(ClientId))
        {
            claims.Add(new Claim(AbpClaimTypes.ClientId, ClientId));
        }

        foreach (var role in Roles)
        {
            claims.Add(new Claim(AbpClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType, AbpClaimTypes.UserName, AbpClaimTypes.Role));
    }
}
