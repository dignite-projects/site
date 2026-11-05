using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Dignite.Site.Themes;

/// <summary>
/// The DDL SQL Server gets for the theme's unique indexes. SQL Server is where it matters: EF Core puts
/// <c>WHERE [TenantId] IS NOT NULL</c> on a unique index over a nullable column unless the mapping sets a filter, and
/// with it the host (TenantId NULL) could have any number of themes. The tests' SQLite database cannot show this - it
/// treats NULLs as distinct either way - so the script is generated from the model without a server.
/// </summary>
public class SiteThemeIndexes_Tests
{
    private static readonly string Script = new SiteDbContext(
            new DbContextOptionsBuilder<SiteDbContext>()
                .UseSqlServer("Server=(none);Database=Site;Trusted_Connection=True")
                .Options)
        .Database.GenerateCreateScript();

    [Fact]
    public void The_Host_Should_Have_One_Theme_Like_A_Tenant()
    {
        Script.ShouldContain("CREATE UNIQUE INDEX [IX_SiteThemes_TenantId] ON [SiteThemes] ([TenantId]);");
    }

    [Fact]
    public void The_Host_Should_Store_A_Content_Once_Like_A_Tenant()
    {
        Script.ShouldContain(
            "CREATE UNIQUE INDEX [IX_SiteThemeBlobs_TenantId_ContentHash] ON [SiteThemeBlobs] ([TenantId], [ContentHash]);");
    }

    /// <summary>That the script carries the filters the mapping does set, so the two above are not passing on a script without any.</summary>
    [Fact]
    public void The_Filters_The_Mapping_Sets_Should_Be_In_The_Script()
    {
        Script.ShouldContain(
            "CREATE UNIQUE INDEX [IX_SiteFileDescriptors_ContainerName_BlobName] ON [SiteFileDescriptors] ([ContainerName], [BlobName]) WHERE TenantId IS NULL;");
        Script.ShouldContain(
            "CREATE UNIQUE INDEX [IX_SitePages_TenantId_Route] ON [SitePages] ([TenantId], [Route]) WHERE IsDeleted = 0;");
    }
}
