using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Common;
using Dignite.Site.ContentTypes;
using Dignite.Site.Pages;
using Dignite.Site.Seo;
using Dignite.Site.Settings;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Settings;

namespace Dignite.Site.Admin.Schema;

/// <summary>
/// Assembles <see cref="SiteSchemaDto"/> (总体设计 §6.2.4).
/// <para>
/// Three <c>[Authorize]</c> attributes, which ASP.NET Core evaluates as AND. The document spans all three
/// of the things they gate, and a merged view must not become a way around any of them: it carries the
/// routing table (<c>Pages</c>), the field arrangements (<c>ContentTypes</c>) and - because a field's
/// definition is merged in whole, configuration included - the field library itself (<c>Fields</c>).
/// Without that last one, a role granted Pages and ContentTypes but explicitly denied Fields would be
/// refused by <c>list_fields</c> and read every definition here instead.
/// </para>
/// <para>
/// Three queries, whatever the site's size - pages, content types, field definitions - because the field
/// merge goes through <see cref="ContentTypeFieldResolver.ResolveManyAsync"/> rather than resolving each
/// type on its own. Content types are fetched unfiltered and grouped in memory: a tenant's whole content
/// type table is exactly what this document contains anyway, and the multi-tenancy filter already scopes
/// it.
/// </para>
/// </summary>
[Authorize(AdminPermissions.Pages.Default)]
[Authorize(AdminPermissions.ContentTypes.Default)]
[Authorize(AdminPermissions.Fields.Default)]
public class SiteSchemaAdminAppService : AdminAppService, ISiteSchemaAdminAppService
{
    protected IPageRepository PageRepository { get; }

    protected IContentTypeRepository ContentTypeRepository { get; }

    protected ContentTypeFieldResolver FieldResolver { get; }

    protected SiteLanguageProvider LanguageProvider { get; }

    protected ISettingProvider SiteSettingProvider { get; }

    public SiteSchemaAdminAppService(
        IPageRepository pageRepository,
        IContentTypeRepository contentTypeRepository,
        ContentTypeFieldResolver fieldResolver,
        SiteLanguageProvider languageProvider,
        ISettingProvider siteSettingProvider)
    {
        PageRepository = pageRepository;
        ContentTypeRepository = contentTypeRepository;
        FieldResolver = fieldResolver;
        LanguageProvider = languageProvider;
        SiteSettingProvider = siteSettingProvider;
    }

    public virtual async Task<SiteSchemaDto> GetAsync()
    {
        var languages = await LanguageProvider.GetEnabledLanguagesAsync();

        var schema = new SiteSchemaDto
        {
            EnabledLanguages = languages.ToList(),
            DefaultLanguage = languages[0],
            PrimaryDomain = await SiteSettingProvider.GetOrNullAsync(SiteSettings.PrimaryDomain) ?? string.Empty
        };

        // Inactive pages are included, carrying their flag. This is an authoring surface: a page that is
        // not live yet is exactly the one a client is most likely to be filling in.
        var pages = await PageRepository.GetListAsync();

        // Every reference in this schema is by name, never a Guid (总体设计 §6.2.4) - built once here
        // rather than re-querying per page, since the whole list is already in memory.
        var pageNamesById = pages.ToDictionary(page => page.Id, page => page.Name);

        var contentTypesByPage = (await ContentTypeRepository.GetListAsync())
            .GroupBy(contentType => contentType.PageId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var resolvedFields = await FieldResolver.ResolveManyAsync(
            contentTypesByPage.Values.SelectMany(types => types));

        foreach (var page in pages)
        {
            var pageDto = new SiteSchemaPageDto
            {
                Name = page.Name,
                DisplayName = page.DisplayName,
                Route = page.Route,
                IsHomePage = page.IsHomePage,
                IsActive = page.IsActive,
                Parent = page.ParentId != null ? pageNamesById.GetValueOrDefault(page.ParentId.Value) : null
            };

            if (contentTypesByPage.TryGetValue(page.Id, out var contentTypes))
            {
                pageDto.ContentTypes = contentTypes
                    .OrderBy(contentType => contentType.Name)
                    .Select(contentType => MapContentType(contentType, resolvedFields[contentType.Id]))
                    .ToList();
            }

            schema.Pages.Add(pageDto);
        }

        return schema;
    }

    protected virtual SiteSchemaContentTypeDto MapContentType(
        ContentType contentType,
        IReadOnlyList<ResolvedContentTypeField> fields)
    {
        return new SiteSchemaContentTypeDto
        {
            Name = contentType.Name,
            DisplayName = contentType.DisplayName,
            Description = contentType.Description,
            Fields = fields.Select(MapField).ToList()
        };
    }

    protected virtual SiteSchemaFieldDto MapField(ResolvedContentTypeField field)
    {
        return new SiteSchemaFieldDto
        {
            // The definition's name, never the usage's display override - this is the value bag's key.
            Name = field.Definition.Name,
            DisplayName = field.DisplayName,
            Description = field.Definition.Description,
            FieldTypeName = field.Definition.FieldTypeName,
            Configuration = field.Definition.Configuration.ToValueDictionary(),
            Required = field.Usage.Required,
            Searchable = field.Usage.Searchable,
            Order = field.Usage.Order,
            ShowInList = field.Usage.ShowInList
        };
    }
}
