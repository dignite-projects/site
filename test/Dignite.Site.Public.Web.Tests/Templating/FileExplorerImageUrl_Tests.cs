using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Templating;

public class FileExplorerImageUrl_Tests
{
    [Fact]
    public void Adds_The_Size_To_A_FileExplorer_Address()
    {
        FileExplorerImageUrl.Sized("https://x/api/file-explorer/files/abc", 1200)
            .ShouldBe("https://x/api/file-explorer/files/abc?Width=1200");
        FileExplorerImageUrl.Sized("https://x/api/file-explorer/files/abc", 400, 300)
            .ShouldBe("https://x/api/file-explorer/files/abc?Width=400&Height=300");
    }

    [Fact]
    public void Replaces_A_Size_Already_There()
    {
        FileExplorerImageUrl.Sized("https://x/api/file-explorer/files/abc?Width=80", 1200)
            .ShouldBe("https://x/api/file-explorer/files/abc?Width=1200");
    }

    [Fact]
    public void Leaves_Other_Addresses_Alone()
    {
        FileExplorerImageUrl.Sized("https://cdn.example.com/a.png", 1200).ShouldBe("https://cdn.example.com/a.png");
        FileExplorerImageUrl.Sized(null, 1200).ShouldBeNull();
        FileExplorerImageUrl.Sized(" ", 1200).ShouldBe(" ");
    }
}
