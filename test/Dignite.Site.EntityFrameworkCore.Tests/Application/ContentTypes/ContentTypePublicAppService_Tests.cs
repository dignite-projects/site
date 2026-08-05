using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.ContentTypes;

public class ContentTypePublicAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IContentTypePublicAppService _contentTypePublicAppService;

    public ContentTypePublicAppService_Tests()
    {
        _contentTypePublicAppService = GetRequiredService<IContentTypePublicAppService>();
    }

    [Fact]
    public async Task Should_Get_Content_Types_Under_A_Page()
    {
        var result = await _contentTypePublicAppService.GetListByPageAsync(SiteTestData.BlogPageId);

        result.Items.Select(ct => ct.Name).ShouldContain("post-article");
        result.Items.Select(ct => ct.Name).ShouldContain("post-gallery");
    }
}
