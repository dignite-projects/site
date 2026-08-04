namespace Dignite.Sites.ContentTypes;

/// <summary>
/// Column lengths for <c>ContentType</c>.
/// </summary>
public static class ContentTypeConsts
{
    /// <summary>Default value: 64. Unique within the owning page.</summary>
    public static int MaxNameLength { get; set; } = 64;

    /// <summary>Default value: 128.</summary>
    public static int MaxDisplayNameLength { get; set; } = 128;

    /// <summary>Default value: 512. Doubles as the type description handed to an AI client over MCP.</summary>
    public static int MaxDescriptionLength { get; set; } = 512;
}
