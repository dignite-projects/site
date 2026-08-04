using System.Linq;
using System.Threading.Tasks;
using Dignite.Sites.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Dignite.Sites.Public.Fields;

public class FieldPublicAppService_Tests : SitesEntityFrameworkCoreTestBase
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
            new[] { SitesTestData.TitleFieldId, SitesTestData.BodyFieldId });

        result.Items.Select(f => f.Name).OrderBy(n => n).ShouldBe(new[] { "body", "title" });
    }
}
