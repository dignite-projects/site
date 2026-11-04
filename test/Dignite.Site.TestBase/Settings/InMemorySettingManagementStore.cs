using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace Dignite.Site.Settings;

/// <summary>
/// Setting Management's store without its database: what <c>ISettingManager</c> writes is what the setting providers read
/// back, and <see cref="Stored"/> says where each value landed, so a test sees the real <c>SettingManager</c> at work -
/// its provider choice and its clearing of a value that equals its fallback - against a few dictionary entries.
/// <para>
/// The test layers do not have Setting Management's tables (the domain and application layers have no database at all), and
/// they turn its two startup features off (<see cref="SettingManagementOptions"/>), as ABP's own tests do, so nothing
/// else of the module reaches for one.
/// </para>
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ISettingManagementStore), typeof(InMemorySettingManagementStore))]
public class InMemorySettingManagementStore : ISettingManagementStore, ISingletonDependency
{
    private readonly object _lock = new();
    private readonly List<StoredSetting> _stored = new();

    /// <summary>The values held, in the order they were first stored.</summary>
    public IReadOnlyList<StoredSetting> Stored
    {
        get
        {
            lock (_lock)
            {
                return _stored.ToList();
            }
        }
    }

    public Task<string> GetOrNullAsync(string name, string providerName, string providerKey)
    {
        lock (_lock)
        {
            return Task.FromResult(Find(name, providerName, providerKey)?.Value)!;
        }
    }

    public Task<List<SettingValue>> GetListAsync(string providerName, string providerKey)
    {
        lock (_lock)
        {
            return Task.FromResult(
                _stored
                    .Where(setting => setting.ProviderName == providerName && setting.ProviderKey == providerKey)
                    .Select(setting => new SettingValue(setting.Name, setting.Value))
                    .ToList());
        }
    }

    public async Task<List<SettingValue>> GetListAsync(string[] names, string providerName, string providerKey)
    {
        return (await GetListAsync(providerName, providerKey)).Where(value => names.Contains(value.Name)).ToList();
    }

    public Task SetAsync(string name, string value, string providerName, string providerKey)
    {
        lock (_lock)
        {
            var existing = Find(name, providerName, providerKey);

            if (existing == null)
            {
                _stored.Add(new StoredSetting(name, value, providerName, providerKey));
            }
            else
            {
                _stored[_stored.IndexOf(existing)] = existing with { Value = value };
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string name, string providerName, string providerKey)
    {
        lock (_lock)
        {
            _stored.RemoveAll(setting => setting.Name == name
                                         && setting.ProviderName == providerName
                                         && setting.ProviderKey == providerKey);
        }

        return Task.CompletedTask;
    }

    private StoredSetting? Find(string name, string providerName, string providerKey) =>
        _stored.FirstOrDefault(setting => setting.Name == name
                                          && setting.ProviderName == providerName
                                          && setting.ProviderKey == providerKey);
}

/// <summary>One value in <see cref="InMemorySettingManagementStore"/>: the provider that holds it ("G" global, "T" tenant) and its key (the tenant's id).</summary>
public sealed record StoredSetting(string Name, string Value, string ProviderName, string? ProviderKey);
