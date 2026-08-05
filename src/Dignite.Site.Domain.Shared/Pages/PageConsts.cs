namespace Dignite.Site.Pages;

/// <summary>
/// Column lengths for <c>Page</c>. Mutable statics (like ABP's own <c>AbpUserConsts</c>) so a host can
/// widen a column before its model is built.
/// </summary>
public static class PageConsts
{
    /// <summary>Default value: 64.</summary>
    public static int MaxNameLength { get; set; } = 64;

    /// <summary>Default value: 128.</summary>
    public static int MaxDisplayNameLength { get; set; } = 128;

    /// <summary>Default value: 256. The base route, e.g. <c>/blog</c>.</summary>
    public static int MaxRouteLength { get; set; } = 256;

    /// <summary>
    /// Default value: 128. The content path pattern, e.g. <c>{slug}</c> or <c>{date:yyyy/MM}/{slug}</c>.
    /// </summary>
    public static int MaxContentPathPatternLength { get; set; } = 128;

    /// <summary>Default value: 256. Optional template reference - only used by back-end-rendered front ends.</summary>
    public static int MaxTemplateLength { get; set; } = 256;
}
