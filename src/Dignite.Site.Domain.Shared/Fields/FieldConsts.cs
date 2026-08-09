namespace Dignite.Site.Fields;

/// <summary>
/// Column lengths for the properties <c>Field</c> adds on top of <c>IFlexField</c>.
/// <para>
/// Only <c>GroupName</c> lives here. <c>Field</c>'s other columns - Name, DisplayName, Description,
/// FieldTypeName, Configuration - are the ones <c>IFlexField</c> defines, so their lengths belong to
/// <c>FlexFieldConsts</c> in the kernel and are applied by <c>ConfigureFlexField&lt;Field&gt;()</c>.
/// Restating them here would create a second set that could drift from the ones actually being mapped.
/// </para>
/// </summary>
public static class FieldConsts
{
    /// <summary>Default value: 64.</summary>
    public static int MaxGroupNameLength { get; set; } = 64;
}
