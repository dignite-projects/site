using Volo.Abp;

namespace Dignite.Site.Fields;

/// <summary>
/// A field definition nests composite field types (<c>Matrix</c>, <c>Table</c>) deeper than
/// <see cref="Dignite.FlexFields.Site.CompositeFieldNesting.MaxDepth"/> allows.
///
/// <para>
/// Raised here rather than left to the designer UI on purpose: the UI stops offering composite types
/// past the limit, but the configuration is a free-form dictionary on the wire, so an API caller can
/// send any shape it likes. This is the constraint; the picker is the courtesy.
/// </para>
/// </summary>
public class FieldNestingTooDeepException : BusinessException
{
    public FieldNestingTooDeepException(string name, int maxDepth)
        : base(SiteErrorCodes.FieldNestingTooDeep)
    {
        WithData("Name", name);
        WithData("MaxDepth", maxDepth);
    }
}
