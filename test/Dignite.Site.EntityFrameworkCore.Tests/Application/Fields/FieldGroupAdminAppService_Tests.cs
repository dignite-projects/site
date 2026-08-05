using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Admin.Fields;

public class FieldGroupAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IFieldGroupAdminAppService _fieldGroupAppService;
    private readonly IFieldAdminAppService _fieldAppService;

    public FieldGroupAdminAppService_Tests()
    {
        _fieldGroupAppService = GetRequiredService<IFieldGroupAdminAppService>();
        _fieldAppService = GetRequiredService<IFieldAdminAppService>();
    }

    [Fact]
    public async Task Should_Reject_Duplicate_Group_Name()
    {
        await _fieldGroupAppService.CreateAsync(new CreateFieldGroupDto { Name = "admin-duplicate-group-test" });

        await Should.ThrowAsync<UserFriendlyException>(() =>
            _fieldGroupAppService.CreateAsync(new CreateFieldGroupDto { Name = "admin-duplicate-group-test" }));
    }

    /// <summary>Database <c>SetNull</c>, not <c>Cascade</c>: a field's own definition must survive its group's deletion.</summary>
    [Fact]
    public async Task Should_Clear_GroupId_On_Its_Fields_When_The_Group_Is_Deleted()
    {
        var group = await _fieldGroupAppService.CreateAsync(new CreateFieldGroupDto { Name = "admin-delete-group-test" });

        var field = await _fieldAppService.CreateAsync(new CreateFieldDto
        {
            Name = "admin-group-member-field",
            DisplayName = "Group member",
            FieldTypeName = "TextEdit",
            GroupId = group.Id
        });

        await _fieldGroupAppService.DeleteAsync(group.Id);

        var reloaded = await _fieldAppService.GetAsync(field.Id);
        reloaded.GroupId.ShouldBeNull();
    }
}
