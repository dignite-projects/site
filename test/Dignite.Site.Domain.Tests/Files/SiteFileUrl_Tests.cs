using Shouldly;
using Xunit;

namespace Dignite.Site.Files;

public class SiteFileUrl_Tests
{
    private const string Managed = "/blobs/images/abc.webp?__tenant=3fa85f64-5717-4562-b3fc-2c963f66afa6";

    [Fact]
    public void Sizes_An_Address_That_Has_No_Query()
    {
        SiteFileUrl.Sized("/blobs/images/a.webp", 1200, 630).ShouldBe("/blobs/images/a.webp?width=1200&height=630");
    }

    /// <summary>A tenant's file names its tenant; sizing it must not cut the tenant off.</summary>
    [Fact]
    public void Keeps_The_Other_Query_Parameters()
    {
        SiteFileUrl.Sized(Managed, 1200, 630).ShouldBe(Managed + "&width=1200&height=630");
        SiteFileUrl.Sized("https://x/blobs/images/a.webp?__tenant=42&width=80", 1024)
            .ShouldBe("https://x/blobs/images/a.webp?__tenant=42&width=1024");
    }

    [Fact]
    public void Replaces_A_Size_Already_On_The_Address()
    {
        SiteFileUrl.Sized("/blobs/images/a.webp?Width=80&HEIGHT=60", 1200, 630).ShouldBe("/blobs/images/a.webp?width=1200&height=630");
        SiteFileUrl.Sized("/blobs/images/a.webp?width=80", null, 300).ShouldBe("/blobs/images/a.webp?height=384");
    }

    /// <summary>
    /// Blob Management serves only the sizes its container allows: one side given alone is rounded up to the next of
    /// <see cref="SiteImageSizes.All"/>, which keeps the picture's proportions; both given are written as asked.
    /// </summary>
    [Theory]
    [InlineData(300, null, "width=384")]
    [InlineData(384, null, "width=384")]
    [InlineData(1100, null, "width=1200")]
    [InlineData(9000, null, "width=4096")]
    [InlineData(null, 600, "height=630")]
    [InlineData(400, 300, "width=400&height=300")]
    public void Rounds_A_Size_Given_Alone_Up_To_An_Allowed_One(int? width, int? height, string query)
    {
        SiteFileUrl.Sized("/blobs/images/a.webp", width, height).ShouldBe("/blobs/images/a.webp?" + query);
    }

    /// <summary>A size not given, or not positive, is left off the address: none at all is the address as it is.</summary>
    [Fact]
    public void Leaves_Off_A_Size_That_Is_Not_Given()
    {
        SiteFileUrl.Sized(Managed).ShouldBe(Managed);
        SiteFileUrl.Sized(Managed, 0, null).ShouldBe(Managed);
        SiteFileUrl.Sized("/blobs/images/a.webp", 0, 630).ShouldBe("/blobs/images/a.webp?height=630");
    }

    /// <summary>
    /// The addresses Site served its files at before 3.0, and the ones File Explorer did before that, are still Site
    /// files: the endpoint redirects them to Blob Management, passing the size on.
    /// </summary>
    [Theory]
    [InlineData("/api/site-public/files/images/a.jpg")]
    [InlineData("https://x/api/site-public/files/images/a.jpg?__tenant=")]
    [InlineData("https://x/api/file-explorer/files/images/a.jpg?__tenant=")]
    public void Still_Sizes_An_Earlier_Address(string earlier)
    {
        SiteFileUrl.IsSiteFile(earlier).ShouldBeTrue();
        SiteFileUrl.Sized(earlier, 1200, 630).ShouldBe(earlier + (earlier.Contains('?') ? "&" : "?") + "width=1200&height=630");
    }

    [Theory]
    [InlineData("/blobs/images/abc.webp")]
    [InlineData("https://blobs.example/blobs/attachments/x.pdf?__tenant=1")]
    public void Recognizes_Blob_Managements_Address(string url)
    {
        SiteFileUrl.IsSiteFile(url).ShouldBeTrue();
    }

    [Theory]
    [InlineData("https://cdn.example.com/a.png")]
    [InlineData("https://cdn.example.com/media/blobs/images/a.png")]
    [InlineData("/blobs/")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Leaves_Other_Addresses_Alone(string? url)
    {
        SiteFileUrl.IsSiteFile(url).ShouldBeFalse();
        SiteFileUrl.Sized(url, 1200).ShouldBe(url);
    }

    [Fact]
    public void Absolutize_Puts_A_Relative_Address_On_The_Base_Url()
    {
        const string relative = "/blobs/images/a.webp?__tenant=1&width=1200";

        SiteFileUrl.Absolutize(relative, "https://acme.example").ShouldBe("https://acme.example" + relative);
        SiteFileUrl.Absolutize(relative, "https://acme.example/").ShouldBe("https://acme.example" + relative);
        // A site hosted under a path keeps it, as SiteUrlContext.BuildAbsolute does.
        SiteFileUrl.Absolutize(relative, "https://acme.example/site").ShouldBe("https://acme.example/site" + relative);
        SiteFileUrl.Absolutize("images/a.jpg", "https://acme.example").ShouldBe("https://acme.example/images/a.jpg");
    }

    [Fact]
    public void Absolutize_Leaves_An_Absolute_Address_Alone()
    {
        SiteFileUrl.Absolutize("https://cdn.example.com/a.png", "https://acme.example").ShouldBe("https://cdn.example.com/a.png");
        SiteFileUrl.Absolutize("HTTP://cdn.example.com/a.png", "https://acme.example").ShouldBe("HTTP://cdn.example.com/a.png");
        SiteFileUrl.Absolutize("//cdn.example.com/a.png", "https://acme.example").ShouldBe("//cdn.example.com/a.png");
        SiteFileUrl.Absolutize("data:image/png;base64,AAAA", "https://acme.example").ShouldBe("data:image/png;base64,AAAA");
        SiteFileUrl.Absolutize(null, "https://acme.example").ShouldBeNull();
        SiteFileUrl.Absolutize(" ", "https://acme.example").ShouldBe(" ");
    }
}
