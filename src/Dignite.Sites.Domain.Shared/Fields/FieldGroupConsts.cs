namespace Dignite.Sites.Fields;

/// <summary>
/// Column lengths for <c>FieldGroup</c>.
/// <para>
/// There is deliberately no <c>FieldConsts</c> beside this. <c>Field</c>'s own columns - Name,
/// DisplayName, Description, FieldTypeName, Configuration - are the ones <c>IFlexField</c> defines, so
/// their lengths belong to <c>FlexFieldConsts</c> in the kernel and are applied by
/// <c>ConfigureFlexField&lt;Field&gt;()</c>. Restating them here would create a second set that could
/// drift from the ones actually being mapped.
/// </para>
/// </summary>
public static class FieldGroupConsts
{
    /// <summary>Default value: 64.</summary>
    public static int MaxNameLength { get; set; } = 64;
}
