using System.Collections.Generic;
using System.Linq;
using Dignite.Abp.BlobManagement;
using Dignite.Site.Seo;

namespace Dignite.Site.Files;

/// <summary>
/// The widths and heights Site asks Blob Management to resize its pictures to: Blob Management's default list
/// (<see cref="BlobManagementPublicUrl.DefaultSizes"/>) and the <c>og:image</c> box (<see cref="OpenGraphConsts"/>).
/// Blob Management answers any other size with 400, so the host allows exactly these on the containers whose pictures
/// Site shows (<c>UseBlobManagementPublicSizes(SiteImageSizes.All)</c>), and <see cref="SiteFileUrl.Sized"/> rounds a
/// size given alone up to one of them.
/// </summary>
public static class SiteImageSizes
{
    public static IReadOnlyList<int> All { get; } = BlobManagementPublicUrl.DefaultSizes
        .Concat(new[] { OpenGraphConsts.ImageWidth, OpenGraphConsts.ImageHeight })
        .Distinct()
        .OrderBy(size => size)
        .ToArray();

    /// <summary>The smallest of <see cref="All"/> at least <paramref name="size"/>; the largest for a size beyond it.</summary>
    public static int RoundUp(int size)
    {
        foreach (var allowed in All)
        {
            if (allowed >= size)
            {
                return allowed;
            }
        }

        return All[^1];
    }
}
