using System;
using System.Collections.Generic;
using Volo.Abp;

namespace Dignite.Site.Files;

/// <summary>
/// The sortings a file listing accepts. The value reaches a dynamic <c>OrderBy</c>, so anything but a known
/// property and direction is refused rather than passed through.
/// </summary>
public static class FileDescriptorSorting
{
    public const string Default = nameof(FileDescriptor.CreationTime) + " desc";

    private static readonly HashSet<string> AllowedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(FileDescriptor.CreationTime),
        nameof(FileDescriptor.Name),
        nameof(FileDescriptor.Size),
        nameof(FileDescriptor.BlobName)
    };

    public static string Normalize(string? sorting, string defaultSorting = Default)
    {
        if (sorting.IsNullOrWhiteSpace())
        {
            return defaultSorting;
        }

        var parts = sorting!.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 2 || !AllowedFields.Contains(parts[0]) ||
            parts.Length == 2 &&
            !parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase) &&
            !parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(SiteErrorCodes.FileSortingNotSupported);
        }

        var direction = parts.Length == 2 ? parts[1].ToLowerInvariant() : "asc";
        return $"{parts[0]} {direction}";
    }
}
