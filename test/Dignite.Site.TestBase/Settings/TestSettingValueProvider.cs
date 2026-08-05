using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace Dignite.Site.Settings;

/// <summary>
/// An in-memory setting override a test can write to.
/// <para>
/// Registered <b>last</b> in <c>AbpSettingOptions.ValueProviders</c>, which is what gives it the final
/// say: <c>SettingProvider</c> walks the providers in reverse registration order and takes the first
/// non-null answer. Holding nothing by default, it answers null for everything and every setting resolves
/// exactly as it would have - which is why <c>SiteSettingDefinitionProvider_Tests</c>' platform-default
/// assertions keep passing with this in the chain.
/// </para>
/// <para>
/// The alternative would be pulling ABP's Setting Management module into the test layer just to write a
/// tenant setting value; this is a few lines instead of a module, and it works in the domain test project
/// too, which has no database for that module to store into.
/// </para>
/// </summary>
public class TestSettingValueProvider : ISettingValueProvider, ISingletonDependency
{
    public const string ProviderName = "Test";

    private readonly ConcurrentDictionary<string, string?> _values = new();

    public string Name => ProviderName;

    /// <summary>Overrides one setting. A null value removes the override rather than forcing null.</summary>
    public void Set(string name, string? value)
    {
        if (value == null)
        {
            _values.TryRemove(name, out _);
        }
        else
        {
            _values[name] = value;
        }
    }

    public void Clear()
    {
        _values.Clear();
    }

    public Task<string?> GetOrNullAsync(SettingDefinition setting)
    {
        return Task.FromResult(_values.TryGetValue(setting.Name, out var value) ? value : null);
    }

    public Task<List<SettingValue>> GetAllAsync(SettingDefinition[] settings)
    {
        return Task.FromResult(
            settings
                .Select(setting => new SettingValue(
                    setting.Name,
                    _values.TryGetValue(setting.Name, out var value) ? value : null))
                .ToList());
    }
}
