using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dignite.Site.Admin.Languages;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Languages;
using Dignite.Site.Settings;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Mcp;

public class SiteLanguageTools_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteLanguageTools _languageTools;
    private readonly FakeSiteSettingWriter _settingWriter;

    public SiteLanguageTools_Tests()
    {
        _languageTools = GetRequiredService<SiteLanguageTools>();
        _settingWriter = GetRequiredService<FakeSiteSettingWriter>();
    }

    [Fact]
    public async Task Should_Set_The_Languages_And_Say_Which_Is_The_Default()
    {
        var languages = await _languageTools.SetLanguagesAsync(new() { "zh-Hans", "en" });

        languages.EnabledLanguages.ShouldBe(new[] { "zh-Hans", "en" });
        languages.DefaultLanguage.ShouldBe("zh-Hans");

        // What site_get_schema, the way a client reads them, says next.
        var schema = await GetRequiredService<Admin.Schema.ISiteSchemaAdminAppService>().GetAsync();
        schema.EnabledLanguages.ShouldBe(new[] { "zh-Hans", "en" });
        schema.DefaultLanguage.ShouldBe("zh-Hans");
    }

    [Fact]
    public async Task Should_Refuse_A_Tag_That_Is_Not_A_Language()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => _languageTools.SetLanguagesAsync(new() { "xx-nope" }));

        exception.Code.ShouldBe(SiteErrorCodes.SiteLanguageNotRecognized);
        _settingWriter.Writes.ShouldBeEmpty();
    }

    /// <summary>Changing the languages moves addresses, so it is not granted with the right to read them.</summary>
    [Fact]
    public void Setting_The_Languages_Should_Need_The_Update_Permission()
    {
        typeof(SiteLanguageTools)
            .GetMethod(nameof(SiteLanguageTools.SetLanguagesAsync))!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ShouldBe(new[] { SiteAdminPermissions.Languages.Update });
    }
}
