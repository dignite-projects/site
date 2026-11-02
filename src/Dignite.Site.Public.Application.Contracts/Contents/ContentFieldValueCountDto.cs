namespace Dignite.Site.Public.Contents;

/// <summary>One group of the contents counted by a field: a value and how many contents carry it.</summary>
public class ContentFieldValueCountDto
{
    /// <summary>
    /// The value as invariant text - what a <c>FlexFieldQueryCondition.Value</c> takes, so it can be handed
    /// back as an <c>Equals</c> condition to list the contents the group counted.
    /// </summary>
    public string Value { get; set; } = default!;

    /// <summary>The number of distinct contents with this value.</summary>
    public int Count { get; set; }
}
