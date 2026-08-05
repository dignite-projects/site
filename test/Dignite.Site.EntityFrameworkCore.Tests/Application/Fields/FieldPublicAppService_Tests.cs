using System.Linq;
using System.Threading.Tasks;
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
}
