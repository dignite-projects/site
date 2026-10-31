using System;
using Microsoft.AspNetCore.DataProtection;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The token of a preview link and the value of the preview cookie: what was protected reads back, and nothing else
/// does - and nothing ever throws.
/// </summary>
public class SiteTemplatePreviewTokenProtector_Tests
{
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly EphemeralDataProtectionProvider _dataProtection = new();

    private SiteTemplatePreviewTokenProtector Protector => new(_dataProtection);

    [Fact]
    public void A_Protected_Ticket_Should_Read_Back()
    {
        var token = Protector.Protect(
            new SiteTemplatePreviewTicket(TenantId, "partials/card", 4), TimeSpan.FromMinutes(10), out var expiresAt);

        expiresAt.ShouldBeInRange(DateTimeOffset.UtcNow.AddMinutes(9), DateTimeOffset.UtcNow.AddMinutes(11));
        Protector.TryUnprotect(token, out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBe(TenantId);
        ticket.TemplateName.ShouldBe("partials/card");
        ticket.VersionNumber.ShouldBe(4);
        ticket.Kind.ShouldBe(Dignite.Site.Templates.SiteTemplateKind.Partial);
    }

    [Fact]
    public void The_Hosts_Ticket_Should_Read_Back_Without_A_Tenant()
    {
        var token = Protector.Protect(new SiteTemplatePreviewTicket(null, "layout", 1), TimeSpan.FromMinutes(1), out _);

        Protector.TryUnprotect(token, out var ticket).ShouldBeTrue();
        ticket!.TenantId.ShouldBeNull();
        ticket.Kind.ShouldBe(Dignite.Site.Templates.SiteTemplateKind.Layout);
    }

    [Fact]
    public void An_Expired_Ticket_Should_Be_Refused()
    {
        var token = Protector.Protect(new SiteTemplatePreviewTicket(null, "Default", 1), TimeSpan.FromSeconds(-1), out _);

        Protector.TryUnprotect(token, out var ticket).ShouldBeFalse();
        ticket.ShouldBeNull();
    }

    [Fact]
    public void A_Tampered_Ticket_Should_Be_Refused()
    {
        var token = Protector.Protect(new SiteTemplatePreviewTicket(null, "Default", 1), TimeSpan.FromMinutes(1), out _);
        var characters = token.ToCharArray();
        var middle = characters.Length / 2;
        characters[middle] = characters[middle] == 'A' ? 'B' : 'A';

        Protector.TryUnprotect(new string(characters), out _).ShouldBeFalse();
    }

    [Fact]
    public void A_Ticket_Protected_With_Other_Keys_Should_Be_Refused()
    {
        var token = new SiteTemplatePreviewTokenProtector(new EphemeralDataProtectionProvider())
            .Protect(new SiteTemplatePreviewTicket(null, "Default", 1), TimeSpan.FromMinutes(1), out _);

        Protector.TryUnprotect(token, out _).ShouldBeFalse();
    }

    [Fact]
    public void A_Value_Protected_For_Another_Purpose_Should_Be_Refused()
    {
        var token = _dataProtection.CreateProtector("Another.Purpose").ToTimeLimitedDataProtector()
            .Protect("{\"t\":\"\",\"n\":\"Default\",\"v\":1}", TimeSpan.FromMinutes(1));

        Protector.TryUnprotect(token, out _).ShouldBeFalse();
    }

    /// <summary>Protected under the right purpose, but not a payload this class writes.</summary>
    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"t\":\"not-a-guid\",\"n\":\"Default\",\"v\":1}")]
    [InlineData("{\"t\":\"\",\"n\":\"\",\"v\":1}")]
    [InlineData("{\"t\":\"\",\"n\":\"Default\",\"v\":0}")]
    [InlineData("{\"t\":\"\",\"n\":\"Default\",\"v\":\"1\"}")]
    [InlineData("null")]
    public void A_Malformed_Payload_Should_Be_Refused(string payload)
    {
        var token = _dataProtection.CreateProtector(SiteTemplatePreviewConsts.ProtectorPurpose).ToTimeLimitedDataProtector()
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
        Protector.TryUnprotect(new string('A', SiteTemplatePreviewTokenProtector.MaxTokenLength + 1), out _).ShouldBeFalse();
    }
}
