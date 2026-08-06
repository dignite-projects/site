using System.Collections.Generic;

namespace Dignite.Site.Admin.Schema;

/// <summary>
/// The whole target shape an AI client needs in order to write content for this site, in one document
/// (总体设计 §6.2.4). This is where "schema is the contract" (§1.2 principle 3) is cashed in: the same
/// field arrangement that gates a write is what the client is handed to generate against.
/// <para>
/// Deliberately a <b>read-only projection</b>, not a write shape. Nothing is created or edited by posting
/// one of these back - the entity tools do that - so it is free to be organized for reading (nested, name
/// -keyed) rather than for round-tripping.
/// </para>
/// <para>
/// Addressed entirely by name. <c>Page.Name</c> is unique per tenant, <c>ContentType.Name</c> per page and
/// <c>Field.Name</c> per tenant, so every level has a natural string key and the client never carries a
/// Guid - which for a language model is pure cost: tokens, plus a large hallucination surface.
/// </para>
/// </summary>
public class SiteSchemaDto
{
    /// <summary>
    /// The site's languages in configured order. <b>The first entry is the default</b> - the one whose
    /// URLs carry no culture prefix (总体设计 §5.5). A write's <c>cultureName</c> should come from this
    /// list rather than being invented, which is how a client avoids guessing between <c>zh-CN</c> and
    /// <c>zh-Hans</c>.
    /// </summary>
    public List<string> EnabledLanguages { get; set; } = new();

    /// <summary>Convenience duplicate of <see cref="EnabledLanguages"/>'s first entry.</summary>
    public string DefaultLanguage { get; set; } = default!;

    /// <summary>
    /// The site's primary absolute origin, e.g. <c>https://acme.com</c>. Empty when the tenant has not
    /// configured one, in which case URLs are built from whatever host a request arrives on.
    /// </summary>
    public string PrimaryDomain { get; set; } = string.Empty;

    /// <summary>Every page, in display order - the site's routing table (总体设计 §3.1).</summary>
    public List<SiteSchemaPageDto> Pages { get; set; } = new();
}
