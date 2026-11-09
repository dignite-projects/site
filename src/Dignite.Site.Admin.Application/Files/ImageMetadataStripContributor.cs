using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// The upload rule that drops the metadata editing software piles into a JPEG or PNG and nothing displaying it reads
/// (<see cref="ImageMetadataStripper"/>), keeping every byte of the image. Added to a container after
/// <c>MaxSize</c> and <c>AllowedContentTypes</c>, so it only ever sees an allowed picture of a bounded size, and
/// before <c>ImageResize</c>, whose decode guard then measures the picture by what is left.
/// <para>
/// A one-way transform like the image contributors: the stored BLOB is an ordinary picture, nothing is undone while
/// reading, and a container can take it on while it already holds BLOBs.
/// </para>
/// </summary>
public class ImageMetadataStripContributor : IBlobPipelineContributor, ITransientDependency
{
    public virtual async Task OnSavingAsync(BlobPipelineContext context)
    {
        var stream = await BlobStreamBuffering.EnsureSeekableAsync(
            context,
            BlobStreamBuffering.GetMaxBufferedBytes(context.Configuration));

        byte[]? stripped;
        if (stream is MemoryStream memory && memory.TryGetBuffer(out var buffer))
        {
            stripped = ImageMetadataStripper.Strip(buffer.AsSpan(0, (int)memory.Length));
        }
        else
        {
            stream.Position = 0;
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy, context.CancellationToken);
            stripped = ImageMetadataStripper.Strip(copy.GetBuffer().AsSpan(0, (int)copy.Length));
        }

        if (stripped == null)
        {
            stream.Position = 0;
            return;
        }

        // Assigned, never disposed here: the pipeline owns the content streams, as with the image contributors.
        context.BlobStream = new MemoryStream(stripped, writable: false);
    }

    public virtual Task OnGettingAsync(BlobPipelineContext context)
    {
        return Task.CompletedTask;
    }
}
