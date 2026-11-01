using System;
using Microsoft.AspNetCore.DataProtection;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The token of a preview link and the value of the preview cookie: what was protected reads back, and nothing else
/// does - and nothing ever throws.
/// </summary>
public class SitePreviewTokenProtector_Tests
{
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ReleaseId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly EphemeralDataProtectionProvider _dataProtection = new();

    private SitePreviewTokenProtector Protector => new(_dataProtection);

    [Fact]
    public void A_Protected_Ticket_Should_Read_Back()
    {
        var token = Protector.Protect(new SitePreviewTicket(TenantId, ReleaseId), TimeSpan.FromMinutes(10), out var expiresAt);

        expiresAt.ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(9), DateTimeOffset.UtcNow.AddMinutes(11));
        Protector.TryUnprotect(token, out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBe(TenantId);
        ticket.ReleaseId.ShouldBe(ReleaseId);
    }

    [Fact]
    public void The_Hosts_Working_Copy_Should_Read_Back_Without_A_Tenant_Or_A_Release()
    {
        var token = Protector.Protect(new SitePreviewTicket(null, null), TimeSpan.FromMinutes(1), out _);

        Protector.TryUnprotect(token, out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBeNull();
        ticket.ReleaseId.ShouldBeNull();
    }

    [Fact]
    public void An_Expired_Ticket_Should_Be_Refused()
    {
        var token = Protector.Protect(new SitePreviewTicket(null, null), TimeSpan.FromSeconds(-1), out _);

        Protector.TryUnprotect(token, out var ticket).ShouldBeFalse();
        ticket.ShouldBeNull();
    }

    [Fact]
    public void A_Tampered_Ticket_Should_Be_Refused()
    {
        var token = Protector.Protect(new SitePreviewTicket(null, null), TimeSpan.FromMinutes(1), out _);
        var characters = token.ToCharArray();
        var middle = characters.Length / 2;
        characters[middle] = characters[middle] == 'A' ? 'B' : 'A';

        Protector.TryUnprotect(new string(characters), out _).ShouldBeFalse();
    }

    [Fact]
    public void A_Ticket_Protected_With_Other_Keys_Should_Be_Refused()
    {
        var token = new SitePreviewTokenProtector(new EphemeralDataProtectionProvider())
            .Protect(new SitePreviewTicket(null, null), TimeSpan.FromMinutes(1), out _);

        Protector.TryUnprotect(token, out _).ShouldBeFalse();
    }

    [Fact]
    public void A_Value_Protected_For_Another_Purpose_Should_Be_Refused()
    {
        var token = _dataProtection.CreateProtector("Another.Purpose").ToTimeLimitedDataProtector()
            .Protect("{\"t\":\"\",\"r\":\"\"}", TimeSpan.FromMinutes(1));

        Protector.TryUnprotect(token, out _).ShouldBeFalse();
    }

    /// <summary>Protected under the right purpose, but not a payload this class writes.</summary>
    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"t\":\"not-a-guid\",\"r\":\"\"}")]
    [InlineData("{\"t\":\"\",\"r\":\"not-a-guid\"}")]
    [InlineData("{\"t\":\"\",\"r\":1}")]
    [InlineData("null")]
    public void A_Malformed_Payload_Should_Be_Refused(string payload)
    {
        var token = _dataProtection.CreateProtector(SitePreviewConsts.ProtectorPurpose).ToTimeLimitedDataProtector()
            .Protect(payload, TimeSpan.FromMinutes(1));

        Protector.TryUnprotect(token, out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("!!!not base64!!!")]
    [InlineData("CfDJ8")]
    public void Garbage_Should_Be_Refused(string? token)
    {
        Protector.TryUnprotect(token, out var ticket).ShouldBeFalse();
        ticket.ShouldBeNull();
    }

    [Fact]
    public void A_Value_Longer_Than_Any_Token_Should_Be_Refused_Undecoded()
    {
        Protector.TryUnprotect(new string('A', SitePreviewTokenProtector.MaxTokenLength + 1), out _).ShouldBeFalse();
    }
}
