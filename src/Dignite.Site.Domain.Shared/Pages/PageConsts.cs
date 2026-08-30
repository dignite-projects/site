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

    /// <summary>
    /// Default value: 512. The route template, e.g. <c>/blog</c> or <c>/blog/{slug}</c> - see
    /// <see cref="PageRoute"/>. Wider than the other lengths here on purpose: a placeholder's
    /// <c>:REGEX</c> segment can be much longer than a plain <c>:FORMAT</c> one - an enum-shaped
    /// constraint like <c>{category:^(electronics|clothing|...)$}</c> grows with every alternative -
    /// and unlike a <c>:FORMAT</c>, that text never reaches a resolved URL (<see cref="PageRoute.Build"/>
    /// strips it), so widening this has no effect on URL length.
    /// </summary>
    public static int MaxRouteLength { get; set; } = 512;

    /// <summary>
    /// Default value: 256. Required template reference - the MVC view rendered for this page, regardless
    /// of whether a request resolves to the page itself (a list/index) or to one piece of content beneath
    /// it; the view is responsible for branching on whether content was resolved (总体设计 §7.3, Tier 0
    /// only - unused by any front end that resolves its own templates).
    /// </summary>
    public static int MaxTemplateLength { get; set; } = 256;

    /// <summary>
    /// No whitespace anywhere in the route - the one rule simple enough to express as a single pattern.
    /// Everything else about a route's shape (placeholders, embedded regex segments, format specifiers)
    /// is <see cref="PageRoute"/>'s own grammar, checked separately by <see cref="PageRoute.IsValid"/>.
    /// A <c>const</c>, not a mutable static like the lengths above - a <c>[RegularExpression]</c>
    /// attribute's pattern must be a compile-time constant.
    /// </summary>
    public const string RoutePattern = @"^\S+$";

    /// <summary>
    /// An MVC view name: letters, digits, underscore or hyphen, optionally grouped into folders with
    /// "/", e.g. <c>Default</c> or <c>Blog/Article</c>. Required - at least one character - since
    /// <see cref="Template"/> is required. "." is deliberately not in the allowed set at all, which also
    /// means "../" path traversal cannot be spelled here, not merely disallowed by a separate check.
    /// </summary>
    public const string TemplatePattern = @"^\/?[A-Za-z0-9_][A-Za-z0-9_\-\/]*$";
}
