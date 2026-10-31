using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// Signs, encrypts and dates a <see cref="SiteTemplatePreviewTicket"/>, and reads one back - the preview token of a
/// link and the value of the preview cookie alike. ASP.NET Core Data Protection under
/// <see cref="SiteTemplatePreviewConsts.ProtectorPurpose"/>, time-limited: the expiry is inside the protected
/// payload, so neither a token nor a cookie can be kept alive past it by whoever holds it.
/// <para>
/// The keys are the application's own Data Protection keys. The site that honors a ticket must be able to read what
/// the administration side issued, so both must share a key ring: automatically in one application, explicitly
/// (<c>PersistKeysTo...</c> with one application name) when several instances serve the site.
/// </para>
/// </summary>
public class SiteTemplatePreviewTokenProtector : ITransientDependency
{
    /// <summary>
    /// Longer than any token this class writes (a few hundred characters); anything longer is refused before it is
    /// decoded.
    /// </summary>
    public const int MaxTokenLength = 4096;

    protected ITimeLimitedDataProtector Protector { get; }

    public SiteTemplatePreviewTokenProtector(IDataProtectionProvider dataProtectionProvider)
    {
        Protector = dataProtectionProvider
            .CreateProtector(SiteTemplatePreviewConsts.ProtectorPurpose)
            .ToTimeLimitedDataProtector();
    }

    /// <summary>
    /// <paramref name="ticket"/> as a URL- and cookie-safe string, accepted until <paramref name="expiresAt"/>, which
    /// is now plus <paramref name="lifetime"/>.
    /// </summary>
    public virtual string Protect(SiteTemplatePreviewTicket ticket, TimeSpan lifetime, out DateTimeOffset expiresAt)
    {
        Check.NotNull(ticket, nameof(ticket));

        expiresAt = DateTimeOffset.UtcNow.Add(lifetime);

        var payload = JsonSerializer.Serialize(new Payload
        {
            TenantId = ticket.TenantId?.ToString("D") ?? string.Empty,
            TemplateName = ticket.TemplateName,
            VersionNumber = ticket.VersionNumber
        });

        return Protector.Protect(payload, expiresAt);
    }

    /// <summary>
    /// The ticket in <paramref name="token"/>, when it is one this application protected and it has not expired.
    /// False for anything else - nothing, garbage, a tampered or an expired value, a value protected under another
    /// purpose or with other keys - and never an exception: a bad ticket is ignored, not reported, so the visitor
    /// sees the site as it is.
    /// </summary>
    public virtual bool TryUnprotect(string? token, [NotNullWhen(true)] out SiteTemplatePreviewTicket? ticket)
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

        if (payload == null || string.IsNullOrWhiteSpace(payload.TemplateName) || payload.VersionNumber <= 0)
        {
            return false;
        }

        Guid? tenantId = null;
        if (!string.IsNullOrEmpty(payload.TenantId))
        {
            if (!Guid.TryParse(payload.TenantId, out var parsed))
            {
                return false;
            }

            tenantId = parsed;
        }

        ticket = new SiteTemplatePreviewTicket(tenantId, payload.TemplateName, payload.VersionNumber);
        return true;
    }

    /// <summary>Compact JSON - <c>{"t":"&lt;tenant id or empty&gt;","n":"&lt;name&gt;","v":&lt;number&gt;}</c> - so a token stays short.</summary>
    private sealed class Payload
    {
        [JsonPropertyName("t")]
        public string? TenantId { get; set; }

        [JsonPropertyName("n")]
        public string? TemplateName { get; set; }

        [JsonPropertyName("v")]
        public int VersionNumber { get; set; }
    }
}
