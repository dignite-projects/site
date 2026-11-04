using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Admin.Languages;

public class SiteLanguagesDto
{
    /// <summary>The languages the site serves, in order.</summary>
    public List<string> EnabledLanguages { get; set; } = new();

    /// <summary>The first of <see cref="EnabledLanguages"/>: the language whose URLs carry no prefix.</summary>
    public string DefaultLanguage { get; set; } = default!;
}

public class UpdateSiteLanguagesDto
{
    /// <summary>
    /// The complete list, in order, as BCP 47 tags such as <c>zh-Hans</c> and <c>en</c>. The first is the default language.
    /// </summary>
    [Required]
    public List<string> EnabledLanguages { get; set; } = new();
}
