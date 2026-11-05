using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Themes;

/// <summary>
/// Signs, encrypts and dates a <see cref="SitePreviewTicket"/>, and reads one back - the token of a preview link and
/// the value of the preview cookie alike. ASP.NET Core Data Protection under <see cref="Purpose"/>, time-limited: the
/// expiry is inside the protected payload, so neither a token nor a cookie can be kept alive past it by whoever holds it.
/// <para>
/// The keys are the application's own Data Protection keys. The site that honors a ticket must be able to read what the
/// administration side issued, so both must share a key ring: automatically in one application, explicitly
/// (<c>PersistKeysTo...</c> with one application name) when the administration API and the site are deployed apart, or
/// when several instances serve the site.
/// </para>
/// <para>
/// Here, in the contracts both sides reference, because the two sides may be two applications: the administration API
/// issues the token, the site reads it.
/// </para>
/// </summary>
public class SitePreviewTokenProtector : ITransientDependency
{
    /// <summary>
    /// The Data Protection purpose of the browser preview's token and cookie: a value protected for any other purpose -
    /// an antiforgery token, an authentication cookie, a <see cref="SitePreviewServiceTokenProtector"/> token - never
    /// reads as a preview ticket here, and the reverse.
    /// </summary>
    public const string Purpose = "Dignite.Site.ThemePreview";

    /// <summary>
    /// Longer than any token this class writes (a few hundred characters); anything longer is refused before it is
    /// decoded.
    /// </summary>
    public const int MaxTokenLength = 4096;

    protected ITimeLimitedDataProtector Protector { get; }

    public SitePreviewTokenProtector(IDataProtectionProvider dataProtectionProvider)
        : this(dataProtectionProvider, Purpose)
    {
    }

    protected SitePreviewTokenProtector(IDataProtectionProvider dataProtectionProvider, string purpose)
    {
        Protector = dataProtectionProvider
            .CreateProtector(purpose)
            .ToTimeLimitedDataProtector();
    }

    /// <summary>
    /// <paramref name="ticket"/> as a URL- and cookie-safe string, accepted until <paramref name="expiresAt"/>, which is
    /// now plus <paramref name="lifetime"/>.
    /// </summary>
    public virtual string Protect(SitePreviewTicket ticket, TimeSpan lifetime, out DateTimeOffset expiresAt)
    {
        Check.NotNull(ticket, nameof(ticket));

        expiresAt = DateTimeOffset.UtcNow.Add(lifetime);

        var payload = JsonSerializer.Serialize(new Payload
        {
            TenantId = ticket.TenantId?.ToString("D") ?? string.Empty,
            ReleaseId = ticket.ReleaseId?.ToString("D") ?? string.Empty
        });

        return Protector.Protect(payload, expiresAt);
    }

    /// <summary>
    /// The ticket in <paramref name="token"/>, when it is one this application protected and it has not expired. False
    /// for anything else - nothing, garbage, a tampered or an expired value, a value protected under another purpose or
    /// with other keys - and never an exception: a bad ticket is ignored, not reported, so the visitor sees the site as
    /// it is.
    /// </summary>
    public virtual bool TryUnprotect(string? token, [NotNullWhen(true)] out SitePreviewTicket? ticket)
    {
        ticket = null;

        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            return false;
        }

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(Protector.Unprotect(token, out _));
        }
        // CryptographicException: tampered, expired, another purpose, unknown key. FormatException: not base64url.
        // JsonException: protected by this purpose but not a payload this class wrote.
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException
                                              or ArgumentException)
        {
            return false;
        }

        if (payload == null
            || !TryParseOptionalGuid(payload.TenantId, out var tenantId)
            || !TryParseOptionalGuid(payload.ReleaseId, out var releaseId))
        {
            return false;
        }

        ticket = new SitePreviewTicket(tenantId, releaseId);
        return true;
    }

    /// <summary>Empty is null; anything else must be a GUID.</summary>
    private static bool TryParseOptionalGuid(string? value, out Guid? result)
    {
        result = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (!Guid.TryParse(value, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    /// <summary>Compact JSON - <c>{"t":"&lt;tenant id or empty&gt;","r":"&lt;release id or empty&gt;"}</c> - so a token stays short.</summary>
    private sealed class Payload
    {
        [JsonPropertyName("t")]
        public string? TenantId { get; set; }

        [JsonPropertyName("r")]
        public string? ReleaseId { get; set; }
    }
}
