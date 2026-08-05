using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Site.ContentTypes;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dignite.Site.EntityFrameworkCore.ValueComparers;

/// <summary>
/// Change tracking for a content type's field arrangement, which is persisted through a value converter
/// into one JSON column.
/// <para>
/// Without this, EF Core compares the converted <c>List&lt;ContentTypeField&gt;</c> by reference and
/// snapshots it by reference too - so the "original" it diffs against is the very list it is diffing,
/// and a change to the arrangement can be silently dropped. EF warns about exactly this case
/// ("collection or enumeration type with a value converter but with no value comparer"). The same
/// reasoning is why FlexFields ships comparers for its own two JSON columns.
/// </para>
/// <para>
/// The snapshot is a new list, which is what gives the change tracker something independent to compare
/// against. Its elements are not cloned, and do not need to be: <see cref="ContentTypeField"/>'s state is
/// only ever replaced wholesale through <c>ContentType.SetFields()</c>, never mutated in place.
/// </para>
/// </summary>
public class ContentTypeFieldListValueComparer : ValueComparer<List<ContentTypeField>>
{
    public ContentTypeFieldListValueComparer()
        : base(
            (a, b) => Compare(a, b),
            list => list.Aggregate(0, (hash, field) => HashCode.Combine(hash, field.GetHashCode())),
            list => list.ToList())
    {
    }

    private static bool Compare(List<ContentTypeField>? a, List<ContentTypeField>? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null)
        {
            return b is null;
        }

        if (b is null)
        {
            return false;
        }

        // Order-sensitive on purpose: the list order is the arrangement, so reordering a content type's
        // fields is a real change even when the same fields are present.
        return a.SequenceEqual(b);
    }
}
