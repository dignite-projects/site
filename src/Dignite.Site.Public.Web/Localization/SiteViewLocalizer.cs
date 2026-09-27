using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Localization;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// Makes <c>@inject IViewLocalizer</c> read <see cref="SiteTemplateResource"/> inside a site template - any
/// view under <c>/Sites/</c>, a tenant's own included - and behave exactly like ASP.NET Core's
/// <see cref="ViewLocalizer"/> everywhere else.
/// <para>
/// Needed because the stock one finds nothing under ABP: it asks
/// <c>IStringLocalizerFactory.Create(baseName, location)</c> for a per-view resource, and ABP hands that
/// overload straight to the <c>.resx</c> factory. Registered by <see cref="SitePublicWebModule"/> in place
/// of the framework's <see cref="IViewLocalizer"/>.
/// </para>
/// </summary>
public class SiteViewLocalizer : IViewLocalizer, IViewContextAware
{
    private readonly ViewLocalizer _defaultLocalizer;
    private readonly IHtmlLocalizer<SiteTemplateResource> _templateLocalizer;
    private IHtmlLocalizer _localizer = default!;

    public SiteViewLocalizer(
        IHtmlLocalizerFactory localizerFactory,
        IWebHostEnvironment hostingEnvironment,
        IHtmlLocalizer<SiteTemplateResource> templateLocalizer)
    {
        _defaultLocalizer = new ViewLocalizer(localizerFactory, hostingEnvironment);
        _templateLocalizer = templateLocalizer;
    }

    public virtual LocalizedHtmlString this[string name] => _localizer[name];

    public virtual LocalizedHtmlString this[string name, params object[] arguments] => _localizer[name, arguments];

    public virtual LocalizedString GetString(string name) => _localizer.GetString(name);

    public virtual LocalizedString GetString(string name, params object[] arguments) => _localizer.GetString(name, arguments);

    public virtual IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
        _localizer.GetAllStrings(includeParentCultures);

    public virtual void Contextualize(ViewContext viewContext)
    {
        ArgumentNullException.ThrowIfNull(viewContext);

        // The same path ViewLocalizer itself goes by: the file being executed, which for a partial or
        // layout is that file rather than the view that pulled it in.
        var path = viewContext.ExecutingFilePath;
        if (string.IsNullOrEmpty(path))
        {
            path = viewContext.View?.Path;
        }

        if (IsSiteTemplate(path))
        {
            _localizer = _templateLocalizer;
            return;
        }

        _defaultLocalizer.Contextualize(viewContext);
        _localizer = _defaultLocalizer;
    }

    protected virtual bool IsSiteTemplate(string? path)
    {
        return path != null &&
               path.StartsWith(TenantViewLocationExpander.SitesFolder + "/", StringComparison.OrdinalIgnoreCase);
    }
}
