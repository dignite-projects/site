using System;

namespace Dignite.Site.Templates;

/// <summary>
/// One row of <see cref="ISiteTemplateRepository.GetPublishedRenderSetAsync"/>: a published version flattened
/// together with the name of its template. A projection and not <see cref="SiteTemplateVersion"/> because an
/// asset's <see cref="Body"/> is deliberately left null - it is not read from the database at all, the
/// renderer only needs the asset's version id for its address - and because the name, which lives on the
/// template, has to travel with the version for the caller to tell the rows apart.
/// </summary>
public sealed record PublishedSiteTemplateVersion(
    string TemplateName,
    Guid Id,
    Guid TemplateId,
    int Number,
    string? Body,
    string? Comment,
    DateTime CreationTime);
