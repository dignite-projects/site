using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Timing;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Signs and reads the tickets of upload links, and remembers which have been used.
/// <para>
/// A ticket is a <see cref="FileUploadTicket"/> as JSON, protected by ASP.NET Core Data Protection: tampered with, or
/// signed by another application's keys, it does not read. That is the key ring the host keeps for its cookies, so a
/// deployment of several instances shares it already. The expiry is inside what is protected, not a second field next to
/// it. A ticket is good for one use: the first redemption records its nonce in the distributed cache until the ticket
/// would have expired, and a second finds it there. Looking and recording are one step under a lock named by the nonce
/// (<see cref="IAbpDistributedLock"/>), so two redemptions at the same moment - on one instance or on several, when the
/// host configures a distributed lock as it configures a distributed cache - are not both the first.
/// </para>
/// </summary>
public class FileUploadTicketProtector : ITransientDependency
{
    /// <summary>How long a link lives. Long enough to find a file and run a command, short enough to be of no use later.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>The lock a ticket's use is recorded under: this prefix and the ticket's nonce.</summary>
    protected const string LockName = "Dignite.Site.Admin.Files.UploadTicket:";

    /// <summary>Changes only with the ticket's format: a ticket protected for another purpose does not read here.</summary>
    protected const string Purpose = "Dignite.Site.Admin.Files.UploadTicket.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected IDataProtectionProvider DataProtectionProvider { get; }

    protected IDistributedCache<FileUploadTicketUse> UseCache { get; }

    protected IAbpDistributedLock DistributedLock { get; }

    protected IClock Clock { get; }

    public FileUploadTicketProtector(
        IDataProtectionProvider dataProtectionProvider,
        IDistributedCache<FileUploadTicketUse> useCache,
        IAbpDistributedLock distributedLock,
        IClock clock)
    {
        DataProtectionProvider = dataProtectionProvider;
        UseCache = useCache;
        DistributedLock = distributedLock;
        Clock = clock;
    }

    /// <summary>The protected, URL-safe text of the ticket.</summary>
    public virtual string Protect(FileUploadTicket ticket)
    {
        return DataProtectionProvider.CreateProtector(Purpose).Protect(JsonSerializer.Serialize(ticket, JsonOptions));
    }

    /// <summary>
    /// The ticket <paramref name="protectedTicket"/> holds, or null when it is not one this application signed, is not of
    /// the current version, or has expired. Which of those is not told: a caller learns nothing about tickets it forged.
    /// </summary>
    public virtual FileUploadTicket? Read(string? protectedTicket)
    {
        if (string.IsNullOrWhiteSpace(protectedTicket))
        {
            return null;
        }

        try
        {
            var json = DataProtectionProvider.CreateProtector(Purpose).Unprotect(protectedTicket);
            var ticket = JsonSerializer.Deserialize<FileUploadTicket>(json, JsonOptions);

            return ticket is { Version: FileUploadTicket.CurrentVersion } && ticket.ExpiresAt > Clock.Now
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

    /// <summary>
    /// Marks the ticket used. True the first time, false for every redemption after - and for one that arrives while another
    /// is being recorded, which is refused rather than waited for: the first of them is the one that uses the link.
    /// </summary>
    public virtual async Task<bool> TryUseAsync(FileUploadTicket ticket)
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
            new FileUploadTicketUse(),
            new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
            {
                AbsoluteExpiration = ticket.ExpiresAt
            });

        return true;
    }
}

/// <summary>The cache entry of a ticket that has been used; its key, the nonce, is all that matters.</summary>
public class FileUploadTicketUse
{
}
