using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Settings;

/// <summary>
/// Stands in for <see cref="ISiteSettingWriter"/>, whose implementation is Setting Management's and needs a database the
/// test layers do not have: records every write in <see cref="Writes"/> and also hands the value to
/// <see cref="TestSettingValueProvider"/>, so what a test sets here is what the next read of the setting returns - the round
/// trip a real store gives.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ISiteSettingWriter), typeof(FakeSiteSettingWriter))]
public class FakeSiteSettingWriter : ISiteSettingWriter, ISingletonDependency
{
    private readonly TestSettingValueProvider _values;

    public FakeSiteSettingWriter(TestSettingValueProvider values)
    {
        _values = values;
    }

    /// <summary>Every <see cref="SetAsync"/>, in order.</summary>
    public List<SiteSettingWrite> Writes { get; } = new();

    public Task SetAsync(string name, string value)
    {
        Writes.Add(new SiteSettingWrite(name, value));
        _values.Set(name, value);

        return Task.CompletedTask;
    }
}

/// <summary>One call of <see cref="ISiteSettingWriter.SetAsync"/>.</summary>
public sealed record SiteSettingWrite(string Name, string Value);
