using System;
using System.Text.Json.Serialization;
using Dignite.Site.Files;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// A partial update: only what the client sends changes. <see cref="DirectoryId"/> tells "not sent" apart
/// from an explicit <c>null</c> (move to the root) through <see cref="DirectoryIdSpecified"/>.
/// </summary>
public class UpdateFileInput
{
    private Guid? _directoryId;

    /// <summary>The new display name; <c>null</c> keeps the current one.</summary>
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxNameLength))]
    public string? Name { get; set; }

    /// <summary>The directory to move the file to; an explicit <c>null</c> moves it to the root.</summary>
    public Guid? DirectoryId
    {
        get => _directoryId;
        set
        {
            _directoryId = value;
            DirectoryIdSpecified = true;
        }
    }

    /// <summary>True when the client sent <see cref="DirectoryId"/>, including an explicit <c>null</c>.</summary>
    [JsonIgnore]
    public bool DirectoryIdSpecified { get; private set; }
}
