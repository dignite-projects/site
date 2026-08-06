using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace Dignite.Site.Mcp.Naming;

/// <summary>
/// The slug footgun, handled once (总体设计 §6.2.4, first footgun).
/// <para>
/// Every write tool takes <c>slug</c> as a <b>required</b> parameter, and an empty string is an allowed
/// value. That is deliberate and it is aimed at a model, not at a person: omitting a field is the most
/// common way a model gets a call wrong, and here omission would not raise "missing parameter" - the
/// default is the empty string, which silently claims the page's own URL (§2.4) or trips the unique
/// constraint. Making it required forces the intent to be stated.
/// </para>
/// <para>
/// Given a non-empty slug, this normalizes it - a model hands over <c>"My Trip!"</c> at least as often as
/// <c>"my-trip"</c>. It uses <c>SlugNormalizer.TryNormalize</c> rather than <c>Normalize</c> for exactly
/// the reason that method exists: input made entirely of punctuation normalizes to nothing, and
/// accepting that empty result would be indistinguishable from a deliberate empty slug. So it fails
/// instead, naming the field, and the client can correct itself in one turn.
/// </para>
/// <para>
/// Note this is the tool layer's own doing, not <c>ContentManager</c>'s. The manager stores a slug as
/// given, which is correct for callers that already produce URL-safe slugs (the admin API, seeding); the
/// dictation path is the one where a raw title arrives, and it is the one that shapes it (§8.3).
/// </para>
/// </summary>
public static class McpSlug
{
    /// <summary>
    /// Shapes the slug a <b>new</b> content is stored under. An empty string is returned unchanged - it
    /// is the page's own single content, and the caller had to state it explicitly to get here.
    /// </summary>
    /// <param name="fieldName">
    /// Which parameter to blame in a validation error. It is not always <c>slug</c> - the update path
    /// reaches here through <see cref="NormalizeReplacement"/> with <c>newSlug</c>, and naming the wrong
    /// one sends a self-correcting client to edit a parameter that was fine.
    /// </param>
    public static string Normalize(string? slug, string fieldName = "slug")
    {
        // Null is rejected, not coerced, and it has to be handled explicitly even though the parameter is
        // declared non-nullable: these values arrive from a JSON payload, where `"slug": null` binds
        // straight through the nullable-reference annotation. Coercing it to empty would be the footgun
        // this whole class exists to close - "the model said nothing" silently becoming "the model said
        // this is the page's own content" - and leaving it unguarded is a NullReferenceException the
        // error filter can only report as an opaque internal error.
        if (slug == null)
        {
            throw new AbpValidationException(
                "A slug is required.",
                new List<ValidationResult>
                {
                    new(
                        "Pass an empty string if this content is meant to be the page's own single "
                        + "content (a home or \"about\" page whose URL is the page's route). Otherwise pass "
                        + "a real slug. Null is not the same as either.",
                        new[] { fieldName })
                });
        }

        if (slug.Length == 0)
        {
            return string.Empty;
        }

        if (!SlugNormalizer.TryNormalize(slug, out var normalized))
        {
            throw new AbpValidationException(
                "The slug could not be turned into a usable URL segment.",
                new List<ValidationResult>
                {
                    new(
                        $"'{slug}' contains no letters or digits, so nothing is left after normalization. "
                        + "Pass a slug containing letters or digits, or pass an empty string if this content "
                        + "is meant to be the page's own single content.",
                        new[] { fieldName })
                });
        }

        return normalized;
    }

    /// <summary>
    /// Shapes a <b>replacement</b> slug, and rejects an empty one.
    /// <para>
    /// On <c>create_content</c> an empty slug is deliberate - the parameter is required, so the caller
    /// had to say it. On an update it cannot be: <c>newSlug</c> is optional and "leave it alone" is
    /// spelled by omitting it, so an empty string is a model reaching for "no change" and getting it
    /// wrong. Accepting it would silently move an existing content onto the page's own URL - exactly the
    /// footgun the required-slug rule closes on the create path, reopened on the update path.
    /// </para>
    /// </summary>
    public static string NormalizeReplacement(string? newSlug)
    {
        // Null and empty mean the same thing here and get the same answer. Falling through to Normalize
        // for null would advise "pass an empty string if this is the page's own content" - advice this
        // method's own guard then rejects, so a self-correcting client would bounce between two
        // contradictory errors.
        if (newSlug is null or { Length: 0 })
        {
            throw new AbpValidationException(
                "A replacement slug cannot be empty.",
                new List<ValidationResult>
                {
                    new(
                        "Omit 'newSlug' entirely to keep the content's current slug - null and an empty "
                        + "string are both rejected here. An empty slug means \"this is the page's own "
                        + "single content\", which is a different content's identity rather than a rename, "
                        + "so it cannot be reached by moving an existing one here.",
                        new[] { "newSlug" })
                });
        }

        return Normalize(newSlug, "newSlug");
    }

    /// <summary>
    /// Shapes a slug used to <b>address</b> an existing content, for a caller that may be echoing back
    /// the string it passed to <c>create_content</c> rather than the slug that was stored.
    /// <para>
    /// Returns null when normalization would not change anything, or when nothing survives it - both mean
    /// there is no second candidate worth a second query. Unlike the write paths this never throws: an
    /// unusable address is a not-found, not a validation failure.
    /// </para>
    /// </summary>
    public static string? NormalizeAddressOrNull(string? slug)
    {
        if (slug.IsNullOrEmpty())
        {
            return null;
        }

        return SlugNormalizer.TryNormalize(slug, out var normalized)
               && !string.Equals(normalized, slug, StringComparison.Ordinal)
            ? normalized
            : null;
    }
}
