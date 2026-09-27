using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.VirtualFileSystem;
using Xunit;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// <see cref="SiteTemplateLocalizationContributor"/> behind ABP's real localizer - the same
/// <c>IStringLocalizer&lt;SiteTemplateResource&gt;</c> a template injects - with the template folders held
/// in memory. GitHub issue #73.
/// </summary>
public class SiteTemplateLocalization_Tests : IDisposable
{
    private static readonly Guid AcmeId = Guid.NewGuid();
    private static readonly Guid GlobexId = Guid.NewGuid();

    private readonly InMemoryFileProvider _files = new();
    private readonly IAbpApplicationWithInternalServiceProvider _application;

    public SiteTemplateLocalization_Tests()
    {
        _application = AbpApplicationFactory.Create<SiteTemplateLocalizationTestModule>(options =>
        {
            options.Services.AddSingleton(_files);
        });
        _application.Initialize();
    }

    public void Dispose()
    {
        _application.Dispose();
    }

    private IStringLocalizer<SiteTemplateResource> Localizer =>
        _application.ServiceProvider.GetRequiredService<IStringLocalizer<SiteTemplateResource>>();

    private IDisposable Tenant(Guid id, string name) =>
        _application.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(id, name);

    private void Write(string? tenantName, string culture, string texts, string? fileName = null)
    {
        var folder = SiteTemplateLocalizationContributor.GetVirtualPath(tenantName);
        _files.Set($"{folder}/{fileName ?? culture + ".json"}", "{\"culture\": \"" + culture + "\", \"texts\": {" + texts + "}}");
    }

    private LocalizedString Get(string culture, string name, params object[] arguments)
    {
        using (CultureHelper.Use(culture))
        {
            return arguments.Length == 0 ? Localizer[name] : Localizer[name, arguments];
        }
    }

    [Fact]
    public void Should_Read_The_Hosts_Texts_Without_A_Tenant()
    {
        Write(null, "ja", "\"Field:blog_category:engineering\": \"開発\"");

        Get("ja", "Field:blog_category:engineering").Value.ShouldBe("開発");
    }

    [Fact]
    public void Should_Read_Only_The_Tenants_Own_Texts()
    {
        Write(null, "ja", "\"Shared\": \"host\", \"Both\": \"host\"");
        Write("acme", "ja", "\"Both\": \"acme\"");

        using (Tenant(AcmeId, "acme"))
        {
            Get("ja", "Both").Value.ShouldBe("acme");

            // Never the host's, even for a key only the host has - the same rule the views follow.
            var hostOnly = Get("ja", "Shared");
            hostOnly.ResourceNotFound.ShouldBeTrue();
            hostOnly.Value.ShouldBe("Shared");
        }
    }

    [Fact]
    public void Should_Keep_Tenants_Apart()
    {
        Write("acme", "en", "\"Greeting\": \"Hello from Acme\"");
        Write("globex", "en", "\"Greeting\": \"Hello from Globex\"");

        using (Tenant(AcmeId, "acme"))
        {
            Get("en", "Greeting").Value.ShouldBe("Hello from Acme");
        }

        using (Tenant(GlobexId, "globex"))
        {
            Get("en", "Greeting").Value.ShouldBe("Hello from Globex");
        }

        Get("en", "Greeting").ResourceNotFound.ShouldBeTrue();
    }

    [Fact]
    public void Should_Fall_Back_Through_Every_Parent_Culture()
    {
        Write(null, "zh", "\"OnlyInZh\": \"中文\"");
        Write(null, "zh-Hant", "\"InZhHant\": \"繁體\"");

        // Two levels up from zh-Hant-TW - further than ABP's own base-culture fallback goes.
        Get("zh-Hant-TW", "OnlyInZh").Value.ShouldBe("中文");
        Get("zh-Hant-TW", "InZhHant").Value.ShouldBe("繁體");
    }

    [Fact]
    public void Should_Fall_Back_To_The_Sites_Default_Language_Last()
    {
        Write(null, "ja", "\"Both\": \"日本語\"");
        Write(null, "zh-Hans", "\"Both\": \"简体\", \"OnlyInDefault\": \"默认\"");

        using (SiteTemplateDefaultCulture.Use("zh-Hans"))
        {
            Get("ja", "Both").Value.ShouldBe("日本語");
            Get("ja", "OnlyInDefault").Value.ShouldBe("默认");
            Get("ja", "Nowhere").ResourceNotFound.ShouldBeTrue();
        }
    }

    [Fact]
    public void Should_Skip_The_Default_Language_Outside_A_Render()
    {
        Write(null, "zh-Hans", "\"OnlyInDefault\": \"默认\"");

        SiteTemplateDefaultCulture.Current.ShouldBeNull();
        Get("ja", "OnlyInDefault").ResourceNotFound.ShouldBeTrue();
    }

    [Fact]
    public void Should_Format_Arguments_In_The_Render_Culture()
    {
        Write(null, "zh-Hans", "\"Archive:Month\": \"{0:yyyy年M月}的文章\"");
        Write(null, "en", "\"Archive:Month\": \"Posts from {0:MMMM yyyy}\"");

        var month = new DateTime(2026, 7, 1);

        Get("zh-Hans", "Archive:Month", month).Value.ShouldBe("2026年7月的文章");
        Get("en", "Archive:Month", month).Value.ShouldBe("Posts from July 2026");
    }

    [Fact]
    public void Should_Reload_A_Changed_File()
    {
        Write(null, "en", "\"Greeting\": \"Hello\"");
        Get("en", "Greeting").Value.ShouldBe("Hello");

        Write(null, "en", "\"Greeting\": \"Hi\"");

        Get("en", "Greeting").Value.ShouldBe("Hi");
    }

    [Fact]
    public void Should_Skip_A_Malformed_File_And_Keep_The_Rest()
    {
        Write(null, "en", "\"Greeting\": \"Hello\"");
        _files.Set($"{SiteTemplateLocalizationContributor.GetVirtualPath(null)}/ja.json", "{ this is not json");

        Get("en", "Greeting").Value.ShouldBe("Hello");
        Get("ja", "Greeting").ResourceNotFound.ShouldBeTrue();
    }

    [Fact]
    public void Should_Find_A_File_Whose_Culture_Is_Not_In_Canonical_Case()
    {
        Write(null, "zh-hans", "\"Greeting\": \"你好\"", fileName: "zh-Hans.json");

        Get("zh-Hans", "Greeting").Value.ShouldBe("你好");
    }

    [Fact]
    public void Should_List_Every_Text_A_Lookup_Would_Find()
    {
        Write(null, "zh", "\"A\": \"zh-a\", \"B\": \"zh-b\"");
        Write(null, "zh-Hant", "\"A\": \"hant-a\"");

        using (CultureHelper.Use("zh-Hant"))
        {
            var all = Localizer.GetAllStrings(includeParentCultures: true).ToDictionary(s => s.Name, s => s.Value);

            all["A"].ShouldBe("hant-a");
            all["B"].ShouldBe("zh-b");
        }
    }
}

[DependsOn(
    typeof(AbpLocalizationModule),
    typeof(AbpMultiTenancyModule),
    typeof(AbpVirtualFileSystemModule))]
public class SiteTemplateLocalizationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var files = context.Services.GetSingletonInstance<InMemoryFileProvider>();

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.Add(new VirtualFileSetInfo(files));
        });

        // The same registration SitePublicWebModule makes.
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<SiteTemplateResource>()
                .Contributors.Add(new SiteTemplateLocalizationContributor());
        });
    }
}
