namespace Dignite.Site.Contents;

/// <summary>
/// One group of <see cref="IContentRepository.GetCountByFieldValueAsync"/>: a value of the field and how many
/// contents carry it.
/// </summary>
/// <param name="Value">
/// The value as invariant text, in the form a <c>FlexFieldQueryCondition.Value</c> takes - so a group can be
/// handed straight back as an <c>Equals</c> condition to list the contents it counted.
/// </param>
/// <param name="Count">The number of distinct contents that carry <paramref name="Value"/>.</param>
public record ContentFieldValueCount(string Value, int Count);
