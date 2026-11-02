using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Select;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Fields;

public class FieldPublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IFieldPublicAppService _fieldPublicAppService;

    public FieldPublicAppService_Tests()
    {
        _fieldPublicAppService = GetRequiredService<IFieldPublicAppService>();
    }

    [Fact]
    public async Task Should_Resolve_Field_Definitions_By_Id()
    {
        var result = await _fieldPublicAppService.GetListAsync(
            new[] { SiteTestData.TitleFieldId, SiteTestData.BodyFieldId });

        result.Items.Select(f => f.Name).OrderBy(n => n).ShouldBe(new[] { "body", "title" });
    }

    /// <summary>
    /// What the Scriban documentation says a Select field's configuration looks like to a template: keys that
    /// carry the field type's name and a dot, options as JSON whose members are named as they were stored.
    /// </summary>
    [Fact]
    public async Task A_Select_Fields_Configuration_Should_Come_Back_As_Stored()
    {
        var result = await _fieldPublicAppService.GetListAsync(new[] { SiteTestData.CategoryFieldId });
        var configuration = result.Items.Single().Configuration;

        SelectConfigurationNames.Options.ShouldBe("Select.Options");
        SelectConfigurationNames.Multiple.ShouldBe("Select.Multiple");
        configuration[SelectConfigurationNames.Multiple].ShouldBe(true);

        var options = configuration[SelectConfigurationNames.Options].ShouldBeOfType<JsonElement>();
        options.GetArrayLength().ShouldBe(3);
        options[0].GetProperty("Text").GetString().ShouldBe("Travel");
        options[0].GetProperty("Value").GetString().ShouldBe("travel");
    }
}
