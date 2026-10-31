using System;

namespace Dignite.Site.Templates;

/// <summary>
/// One row of <see cref="ISiteTemplateRepository.GetCurrentVersionsAsync"/>: the version of a template that
/// says what it is <i>now</i> - the published one, or the newest one when the template has never been
/// published - flattened together with its template's name and pointer. What answering "which templates
/// refer to this partial" reads: a template's whole history is not, and a draft-only template is still
/// work its author will publish, so its references count too.
/// <para>
/// A projection, not <see cref="SiteTemplateVersion"/>, for the same reasons as
/// <see cref="PublishedSiteTemplateVersion"/>: the name lives on the template, and an asset's
/// <see cref="Body"/> is not read from the database at all (empty here) - an asset is never parsed, so
/// nothing that reads this row needs it.
/// </para>
/// </summary>
/// <param name="TemplateId">The template's id.</param>
/// <param name="TemplateName">The template's name, as stored.</param>
/// <param name="PublishedVersionNumber">The template's live version, or null when it was never published.</param>
/// <param name="Id">The id of the version in this row.</param>
/// <param name="Number">The number of the version in this row.</param>
/// <param name="IsPublished">Whether the version in this row is the live one.</param>
/// <param name="Body">The version's source; empty for an asset.</param>
public sealed record SiteTemplateCurrentVersion(
    Guid TemplateId,
    string TemplateName,
    int? PublishedVersionNumber,
    Guid Id,
    int Number,
    bool IsPublished,
    string Body);
