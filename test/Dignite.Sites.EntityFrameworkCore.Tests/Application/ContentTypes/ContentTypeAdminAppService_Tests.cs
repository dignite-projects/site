using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Sites.ContentTypes;
using Dignite.Sites.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Sites.Admin.ContentTypes;

public class ContentTypeAdminAppService_Tests : SitesEntityFrameworkCoreTestBase
{
    private readonly IContentTypeAdminAppService _contentTypeAppService;

    public ContentTypeAdminAppService_Tests()
    {
        _contentTypeAppService = GetRequiredService<IContentTypeAdminAppService>();
    }

    [Fact]
    public async Task Should_Get_Seeded_Content_Type_With_Its_Field_Arrangement()
    {
        var contentType = await _contentTypeAppService.GetAsync(SitesTestData.PostArticleTypeId);

        contentType.Name.ShouldBe("post-article");
        contentType.PageId.ShouldBe(SitesTestData.BlogPageId);
        contentType.Fields.Count.ShouldBe(5);

        // Order 0 is title, required and searchable - the per-usage flags the design doc calls out as
        // living on the reference, not the field definition (总体设计 §2.3).
        var title = contentType.Fields.Single(f => f.FieldId == SitesTestData.TitleFieldId);
        title.Required.ShouldBeTrue();
        title.Searchable.ShouldBeTrue();
        title.Order.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Round_Trip_The_Field_Arrangement_Through_Create()
    {
        var fields = new List<ContentTypeFieldDto>
        {
            new() { FieldId = SitesTestData.BodyFieldId, Order = 1, ShowInList = false },
            new() { FieldId = SitesTestData.TitleFieldId, Required = true, Searchable = true, Order = 0, DisplayName = "Headline" }
        };

        var created = await _contentTypeAppService.CreateAsync(new CreateContentTypeDto
        {
            PageId = SitesTestData.BlogPageId,
            Name = "admin-arrangement-test",
            DisplayName = "Arrangement Test",
            Fields = fields
        });

        // SetFields sorts by Order - title (order 0) comes first even though it was given second.
        created.Fields.Count.ShouldBe(2);
        created.Fields[0].FieldId.ShouldBe(SitesTestData.TitleFieldId);
        created.Fields[0].DisplayName.ShouldBe("Headline");
        created.Fields[1].FieldId.ShouldBe(SitesTestData.BodyFieldId);
    }

    [Fact]
    public async Task Should_Reject_Deleting_A_Content_Type_That_Still_Has_Contents()
    {
        await Should.ThrowAsync<UserFriendlyException>(
            () => _contentTypeAppService.DeleteAsync(SitesTestData.PostArticleTypeId));
    }

    [Fact]
    public async Task Should_Delete_A_Content_Type_With_No_Contents()
    {
        var created = await _contentTypeAppService.CreateAsync(new CreateContentTypeDto
        {
            PageId = SitesTestData.BlogPageId,
            Name = "admin-empty-delete-test",
            DisplayName = "Empty"
        });

        await _contentTypeAppService.DeleteAsync(created.Id);

        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(
            () => _contentTypeAppService.GetAsync(created.Id));
    }
}
