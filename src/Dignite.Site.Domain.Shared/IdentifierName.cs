using System.Text.RegularExpressions;

namespace Dignite.Site;

/// <summary>
/// The shape every "identifier" <c>Name</c> follows: <see cref="Pages.Page.Name"/>,
/// <see cref="ContentTypes.ContentType.Name"/>, <see cref="Fields.Field.Name"/> - a stable handle for MCP
/// tools, templates and (for <c>Field</c>) the value bag's own key (总体设计 §6.2.3 "名字寻址"). Lowercase
/// letters and digits, with hyphen or underscore as separators, e.g. <c>post-article</c>, <c>seo</c>,
/// <c>my_field</c>.
/// <para>
/// A plain <c>const</c>, not a mutable static like <see cref="Pages.PageConsts"/>'s lengths - a
/// <c>[RegularExpression]</c> attribute's pattern must be a compile-time constant, so unlike a column
/// length this cannot be widened by a host at startup.
/// </para>
/// </summary>
public static class IdentifierName
{
    public const string Pattern = @"^[a-z0-9][a-z0-9_-]*$";

    private static readonly Regex Regex = new(Pattern, RegexOptions.Compiled);

    public static bool IsValid(string value) => Regex.IsMatch(value);
}
