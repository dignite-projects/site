using System;
using Shouldly;
using Xunit;

namespace Dignite.Site.Files;

public class SiteFileUrl_Tests
{
    [Fact]
    public void Builds_The_Public_Read_Address_With_The_Tenant()
    {
        var tenantId = Guid.Parse("8c6f0e64-6b8f-4c1b-9d5e-1f7d8c4f5a10");

        SiteFileUrl.Build("https://x/", SiteFileContainerNames.Images, "abc", tenantId)
            .ShouldBe($"https://x/api/site-public/files/site-images/abc?__tenant={tenantId}");
        // The host has no tenant; __tenant is still there, empty, so the address always names its tenant.
        SiteFileUrl.Build("https://x", SiteFileContainerNames.Default, "abc", null)
            .ShouldBe("https://x/api/site-public/files/site-files/abc?__tenant=");
    }

    [Fact]
    public void Adds_The_Size_To_A_Site_File_Address()
    {
        SiteFileUrl.Sized("https://x/api/site-public/files/abc", 1200)
            .ShouldBe("https://x/api/site-public/files/abc?Width=1200");
        SiteFileUrl.Sized("https://x/api/site-public/files/abc", 400, 300)
            .ShouldBe("https://x/api/site-public/files/abc?Width=400&Height=300");
    }

    [Fact]
    public void Replaces_A_Size_Already_On_The_Address()
    {
        SiteFileUrl.Sized("https://x/api/site-public/files/abc?Width=80", 1200)
            .ShouldBe("https://x/api/site-public/files/abc?Width=1200");
        SiteFileUrl.Sized("https://x/api/site-public/files/abc?width=80&HEIGHT=60", 1200, 630)
            .ShouldBe("https://x/api/site-public/files/abc?Width=1200&Height=630");
    }

    /// <summary>
    /// Addresses are issued with <c>?__tenant=</c>; sizing an image must not cut the tenant off the address
    /// it was given.
    /// </summary>
    [Fact]
    public void Keeps_The_Other_Query_Parameters()
    {
        SiteFileUrl.Sized("https://x/api/site-public/files/site-images/a.jpg?__tenant=42", 1200, 630)
            .ShouldBe("https://x/api/site-public/files/site-images/a.jpg?__tenant=42&Width=1200&Height=630");
        SiteFileUrl.Sized("https://x/api/site-public/files/site-images/a.jpg?__tenant=&Width=80", 1200)
            .ShouldBe("https://x/api/site-public/files/site-images/a.jpg?__tenant=&Width=1200");
    }

    /// <summary>
    /// The address the files had before Site took them over is still sized for one version, for values
    /// stored before the host ran the data migration that rewrites them (see <see cref="SiteFileUrl"/>).
    /// </summary>
    [Fact]
    public void Still_Sizes_A_Pre_Migration_Address()
    {
        const string legacy = "https://x/api/file-explorer/files/site-images/a.jpg?__tenant=";

        SiteFileUrl.IsSiteFile(legacy).ShouldBeTrue();
        SiteFileUrl.Sized(legacy, 1200, 630).ShouldBe(legacy + "&Width=1200&Height=630");
    }

    [Fact]
    public void Leaves_Other_Addresses_Alone()
    {
        SiteFileUrl.Sized("https://cdn.example.com/a.png", 1200).ShouldBe("https://cdn.example.com/a.png");
        SiteFileUrl.Sized(null, 1200).ShouldBeNull();
        SiteFileUrl.Sized(" ", 1200).ShouldBe(" ");
        SiteFileUrl.IsSiteFile("https://cdn.example.com/a.png").ShouldBeFalse();
    }
}
