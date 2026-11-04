using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.SettingManagement;
using Xunit;

namespace Dignite.Site.Settings;

/// <summary>
/// Where a site's setting is written: for the tenant, kept as the tenant's own, or - the host's own site - as the Global value,
/// the default of every site that sets none. Setting Management is a stand-in here; its module needs a database.
/// </summary>
public class SettingManagerSiteSettingWriter_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly RecordingSettingManager _settingManager = new();

    private SettingManagerSiteSettingWriter CreateWriter() =>
        new(_settingManager, GetRequiredService<ICurrentTenant>());

    [Fact]
    public async Task Should_Write_The_Host_Sites_Value_As_The_Global_One()
    {
        await CreateWriter().SetAsync(SiteSettings.EnabledLanguages, "en,ja");

        var write = _settingManager.Writes.ShouldHaveSingleItem();
        write.Name.ShouldBe(SiteSettings.EnabledLanguages);
        write.Value.ShouldBe("en,ja");
        write.ProviderName.ShouldBe("G");
        write.ProviderKey.ShouldBeNull();
    }

    /// <summary>Forced, so a tenant that sets the very value the host has keeps it when the host changes its own.</summary>
    [Fact]
    public async Task Should_Write_A_Tenants_Value_For_The_Tenant_And_Keep_It_Its_Own()
    {
        var tenantId = Guid.NewGuid();

        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            await CreateWriter().SetAsync(SiteSettings.EnabledLanguages, "ja");
        }

        var write = _settingManager.Writes.ShouldHaveSingleItem();
        write.ProviderName.ShouldBe("T");
        write.ProviderKey.ShouldBe(tenantId.ToString());
        write.Forced.ShouldBeTrue();
    }

    private sealed record Write(string Name, string? Value, string ProviderName, string? ProviderKey, bool Forced);

    private sealed class RecordingSettingManager : ISettingManager
    {
        public List<Write> Writes { get; } = new();

        public Task<string?> GetOrNullAsync(string name, string providerName, string? providerKey, bool fallback = true) =>
            Task.FromResult<string?>(null);

        public Task<List<SettingValue>> GetAllAsync(string providerName, string? providerKey, bool fallback = true) =>
            Task.FromResult(new List<SettingValue>());

        public Task SetAsync(string name, string? value, string providerName, string? providerKey, bool forceToSet = false)
        {
            Writes.Add(new Write(name, value, providerName, providerKey, forceToSet));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string providerName, string? providerKey) => Task.CompletedTask;
    }
}
