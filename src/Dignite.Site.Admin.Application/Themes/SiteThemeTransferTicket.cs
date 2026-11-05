using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Security.Claims;
using Volo.Abp.Timing;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// What a theme download or upload link says, signed inside it (<see cref="SiteThemeTransferTicketProtector"/>): who it
/// was issued to, which way it goes, and until when - as a file upload link does (<c>FileUploadTicket</c>). The link is
/// used by a command line with no credential of its own, so it carries the identity the transfer runs as, and that
/// identity's permissions are checked again when the link is used.
/// </summary>
public sealed record SiteThemeTransferTicket(
    int Version,
    string Nonce,
    DateTimeOffset ExpiresAt,
    Guid? TenantId,
    Guid? UserId,
    string? UserName,
    string[] Roles,
    string? ClientId,
    bool IsUpload,
    Guid? ReleaseId,
    bool Replace)
{
    public const int CurrentVersion = 1;

    /// <summary>The authentication type of <see cref="ToPrincipal"/>, so a log can tell a theme link's identity from a sign-in.</summary>
    public const string AuthenticationType = "SiteThemeTransferTicket";

    /// <summary>The identity the transfer runs as: the claims the permission checks read, nothing else.</summary>
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

/// <summary>
/// Signs and reads the tickets of theme links, and remembers which have been used - the same as
/// <c>FileUploadTicketProtector</c> does for file upload links, under a purpose, a lock and a cache entry of its own:
/// protected with the host's Data Protection keys (so every instance of a deployment reads them), the expiry inside what
/// is protected, and one use, recorded under a distributed lock in the distributed cache until the ticket would have
/// expired.
/// </summary>
public class SiteThemeTransferTicketProtector : ITransientDependency
{
    /// <summary>How long a link lives. Long enough to run a command, short enough to be of no use later.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    protected const string LockName = "Dignite.Site.Admin.Themes.TransferTicket:";

    /// <summary>Changes only with the ticket's format: a ticket protected for another purpose does not read here.</summary>
    protected const string Purpose = "Dignite.Site.Admin.Themes.TransferTicket.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected IDataProtectionProvider DataProtectionProvider { get; }
    protected IDistributedCache<SiteThemeTransferTicketUse> UseCache { get; }
    protected IAbpDistributedLock DistributedLock { get; }
    protected IClock Clock { get; }

    public SiteThemeTransferTicketProtector(
        IDataProtectionProvider dataProtectionProvider,
        IDistributedCache<SiteThemeTransferTicketUse> useCache,
        IAbpDistributedLock distributedLock,
        IClock clock)
    {
        DataProtectionProvider = dataProtectionProvider;
        UseCache = useCache;
        DistributedLock = distributedLock;
        Clock = clock;
    }

    /// <summary>The protected, URL-safe text of the ticket.</summary>
    public virtual string Protect(SiteThemeTransferTicket ticket)
    {
        return DataProtectionProvider.CreateProtector(Purpose).Protect(JsonSerializer.Serialize(ticket, JsonOptions));
    }

    /// <summary>The ticket, or null when it is not one this application signed, is not of the current version, or has expired.</summary>
    public virtual SiteThemeTransferTicket? Read(string? protectedTicket)
    {
        if (string.IsNullOrWhiteSpace(protectedTicket))
        {
            return null;
        }

        try
        {
            var json = DataProtectionProvider.CreateProtector(Purpose).Unprotect(protectedTicket);
            var ticket = JsonSerializer.Deserialize<SiteThemeTransferTicket>(json, JsonOptions);
            return ticket is { Version: SiteThemeTransferTicket.CurrentVersion } && ticket.ExpiresAt > Clock.Now
                ? ticket
                : null;
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException
                                              or JsonException
                                              or FormatException
                                              or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Marks the ticket used: true the first time, false for every use after, and for one racing the first.</summary>
    public virtual async Task<bool> TryUseAsync(SiteThemeTransferTicket ticket)
    {
        await using var handle = await DistributedLock.TryAcquireAsync(LockName + ticket.Nonce);
        if (handle == null)
        {
            return false;
        }

        if (await UseCache.GetAsync(ticket.Nonce) != null)
        {
            return false;
        }

        await UseCache.SetAsync(
            ticket.Nonce,
            new SiteThemeTransferTicketUse(),
            new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions { AbsoluteExpiration = ticket.ExpiresAt });

        return true;
    }
}

/// <summary>The cache entry of a ticket that has been used; its key, the nonce, is all that matters.</summary>
public class SiteThemeTransferTicketUse
{
}
