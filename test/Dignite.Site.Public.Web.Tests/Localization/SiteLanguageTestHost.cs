using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// One request against a site whose <c>Site.EnabledLanguages</c> is <c>enabledLanguages</c> - the pieces the
/// language helpers take from DI, without an application around them.
/// </summary>
public class SiteLanguageTestHost
{
    public SiteLanguageTestHost(string enabledLanguages, string path = "/", string pathBase = "")
    {
        Settings = new FakeSettingProvider { [SiteSettings.EnabledLanguages] = enabledLanguages };
        CurrentTenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);
        Options = Microsoft.Extensions.Options.Options.Create(new SiteLanguageOptions());

        HttpContext = new DefaultHttpContext();
        HttpContext.Request.Method = HttpMethods.Get;
        HttpContext.Request.Path = path;
        HttpContext.Request.PathBase = pathBase;

        LanguageContext = new SiteLanguageContext(
            Settings,
            CurrentTenant,
            new HttpContextAccessor { HttpContext = HttpContext });

        HttpContext.RequestServices = new ServiceCollection()
            .AddSingleton(LanguageContext)
            .AddSingleton(Options)
            .BuildServiceProvider();
    }

    public FakeSettingProvider Settings { get; }

    public ICurrentTenant CurrentTenant { get; }

    public IOptions<SiteLanguageOptions> Options { get; }

    public DefaultHttpContext HttpContext { get; }

    public SiteLanguageContext LanguageContext { get; }
}

public class FakeSettingProvider : ISettingProvider
{
    private readonly Dictionary<string, string?> _values = new();

    public int ReadCount { get; private set; }

    public Exception? Failure { get; set; }

    public string? this[string name]
    {
        set => _values[name] = value;
    }

    public Task<string?> GetOrNullAsync(string name)
    {
        ReadCount++;

        if (Failure != null)
        {
            throw Failure;
        }

        return Task.FromResult(_values.GetValueOrDefault(name));
    }

    public Task<List<SettingValue>> GetAllAsync(string[] names)
    {
        return Task.FromResult(names.Select(name => new SettingValue(name, _values.GetValueOrDefault(name))).ToList());
    }

    public Task<List<SettingValue>> GetAllAsync()
    {
        return Task.FromResult(_values.Select(pair => new SettingValue(pair.Key, pair.Value)).ToList());
    }
}
