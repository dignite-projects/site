using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Site.Public.Files;

/// <summary>
/// A file the public read endpoint serves, wherever it is kept (<see cref="FilePublicAppService"/>): the Drive's, or a
/// descriptor of the file library Site kept before.
/// </summary>
/// <param name="ContainerName">The container in the address.</param>
/// <param name="ContentId">
/// What identifies the stored bytes - the physical BLOB name - so that the renditions of two addresses with the same
/// bytes are one entry of <see cref="RenderedImageCache"/>.
/// </param>
/// <param name="Name">The file's name, which a download is saved under.</param>
/// <param name="MimeType">The type of the stored bytes.</param>
/// <param name="OpenAsync">Opens the stored bytes, or answers <c>null</c> when they are missing from storage.</param>
public record SiteStoredFile(
    string ContainerName,
    string ContentId,
    string Name,
    string MimeType,
    Func<CancellationToken, Task<Stream?>> OpenAsync);
