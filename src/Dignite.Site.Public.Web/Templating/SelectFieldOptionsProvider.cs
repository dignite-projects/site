using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Select;
using Dignite.Site.Public.Application.Contracts.Routing;
using Dignite.Site.Public.ContentTypes;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Pages;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Templating;

/// <summary>
/// Every option of a Select field, read from the field's definition through a page whose content type
/// uses it - for a template that lists them all, such as a category link bar above an archive.
/// <para>
/// A content's own chosen options come from
/// <see cref="ContentTemplateExtensions.GetSelectedOptions"/> instead; this is for the options no
/// content in view may carry - a link bar still has to show a category with no contents yet, including
/// the one being viewed when its archive is empty.
/// </para>
/// </summary>
public class SelectFieldOptionsProvider : ITransientDependency
{
    protected IPagePublicAppService PageAppService { get; }

    protected IContentTypePublicAppService ContentTypeAppService { get; }

    protected IFieldPublicAppService FieldAppService { get; }

    public SelectFieldOptionsProvider(
        IPagePublicAppService pageAppService,
        IContentTypePublicAppService contentTypeAppService,
        IFieldPublicAppService fieldAppService)
    {
        PageAppService = pageAppService;
        ContentTypeAppService = contentTypeAppService;
        FieldAppService = fieldAppService;
    }

    /// <summary>
    /// The options of Select field <paramref name="fieldName"/> in the content type of page
    /// <paramref name="pageName"/>; empty when the page, or a field of that name on it, does not exist.
    /// </summary>
    public virtual async Task<IReadOnlyList<SelectListItem>> GetAsync(string pageName, string fieldName)
    {
        var page = await PageAppService.FindByNameAsync(pageName);
        if (page == null)
        {
            return Array.Empty<SelectListItem>();
        }

        var contentTypes = await ContentTypeAppService.GetListByPageAsync(page.Id);
        var fieldIds = contentTypes.Items.SelectMany(ct => ct.Fields).Select(f => f.FieldId).Distinct().ToList();
        if (fieldIds.Count == 0)
        {
            return Array.Empty<SelectListItem>();
        }

        var field = (await FieldAppService.GetListAsync(fieldIds)).Items.FirstOrDefault(f => f.Name == fieldName);
        return field == null
            ? Array.Empty<SelectListItem>()
            : new SelectConfiguration(field.Configuration.ToFieldConfiguration()).Options;
    }
}
