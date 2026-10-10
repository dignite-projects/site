using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Files;

public class FileDescriptorSorting_Tests
{
    [Fact]
    public void Normalize_Should_Allow_A_Known_Field_And_Direction()
    {
        FileDescriptorSorting.Normalize("Name DESC").ShouldBe("Name desc");
        FileDescriptorSorting.Normalize("size").ShouldBe("size asc");
        FileDescriptorSorting.Normalize(null).ShouldBe(FileDescriptorSorting.Default);
    }

    /// <summary>The value reaches a dynamic OrderBy: anything but "field [asc|desc]" is refused.</summary>
    [Theory]
    [InlineData("Name desc, Size asc")]
    [InlineData("Name.ToString()")]
    [InlineData("Unknown asc")]
    [InlineData("Name descending")]
    public void Normalize_Should_Reject_Unsupported_Sorting(string sorting)
    {
        var exception = Should.Throw<BusinessException>(() => FileDescriptorSorting.Normalize(sorting));

        exception.Code.ShouldBe(SiteErrorCodes.FileSortingNotSupported);
    }
}
