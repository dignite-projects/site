using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Common;
using Dignite.Site.ContentTypes;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Pages;

[Authorize(AdminPermissions.Pages.Default)]
public class PageAdminAppService : AdminAppService, IPageAdminAppService
{
    protected IPageRepository PageRepository { get; }

    protected PageManager PageManager { get; }

    protected ContentTypeManager ContentTypeManager { get; }

    protected IFieldRepository FieldRepository { get; }

    public PageAdminAppService(
        IPageRepository pageRepository,
        PageManager pageManager,
        ContentTypeManager contentTypeManager,
        IFieldRepository fieldRepository)
    {
        PageRepository = pageRepository;
        PageManager = pageManager;
        ContentTypeManager = contentTypeManager;
        FieldRepository = fieldRepository;
    }

    public virtual async Task<PageDto> GetAsync(Guid id)
    {
        var page = await PageRepository.GetAsync(id);
        return MapToDto(page);
    }

    public virtual async Task<PageDto?> FindByNameAsync(string name)
    {
        var page = await PageRepository.FindByNameAsync(name, includeDetails: true);
        return page == null ? null : MapToDto(page);
    }

    public virtual async Task<ListResultDto<PageDto>> GetListAsync(GetPageListInput input)
    {
        var pages = await PageRepository.GetListAsync(isActive: input.IsActive, filter: input.Filter);

        var items = pages.Select(page => ObjectMapper.Map<Page, PageDto>(page)).ToList();

        return new ListResultDto<PageDto>(items);
    }

    [Authorize(AdminPermissions.Pages.Create)]
    public virtual async Task<PageDto> CreateAsync(CreatePageDto input)
    {
        var page = await PageManager.CreateAsync(
            name: input.Name,
            displayName: input.DisplayName,
            route: input.Route,
            template: input.Template,
            isActive: input.IsActive,
            parentId: input.ParentId);

        await CreateDefaultContentTypeAsync(page);

        return MapToDto(page);
    }

    /// <summary>
    /// A page never used to be creatable into anything: Content always needs an existing ContentType
    /// (ContentManager.CreateAsync takes no PageId of its own - it is resolved from the type), so a fresh
    /// page with none yet was a dead end the Contents screen could not create into. This closes that gap
    /// with one content type named after the page itself, carrying the platform's SEO field preset when
    /// one is seeded - matching the SEO-bearing types in 总体设计 §2.3's own example.
    /// <para>
    /// Silently skipped, not failed, when that preset is absent. It is optional platform furniture a data
    /// seeder is responsible for; page creation recreating it here would duplicate that responsibility and
    /// risk a second, divergent definition, and refusing to create the page over a missing nice-to-have
    /// would be a worse failure than simply proceeding without it.
    /// </para>
    /// </summary>
    protected virtual async Task CreateDefaultContentTypeAsync(Page page)
    {
        // ContentTypeManager.CreateAsync re-validates the page exists through its own repository read,
        // which would not see this page yet - nothing reaches the database until the ambient unit of
        // work completes at the end of this call - without flushing what PageManager.CreateAsync just
        // tracked.
        if (UnitOfWorkManager.Current != null)
        {
            await UnitOfWorkManager.Current.SaveChangesAsync();
        }

        var seoField = await FieldRepository.FindByNameAsync(SeoFieldNames.FieldName);
        var fields = seoField == null ? null : new[] { new ContentTypeField(seoField.Id) };

        await ContentTypeManager.CreateAsync(page.Id, page.Name, page.DisplayName, fields: fields);
    }

    [Authorize(AdminPermissions.Pages.Update)]
    public virtual async Task<PageDto> UpdateAsync(Guid id, UpdatePageDto input)
    {
        var page = await PageRepository.GetAsync(id);

        page = await PageManager.UpdateAsync(
            page,
            name: input.Name,
            displayName: input.DisplayName,
            route: input.Route,
            template: input.Template,
            isActive: input.IsActive,
            parentId: input.ParentId);

        return MapToDto(page);
    }

    [Authorize(AdminPermissions.Pages.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Through the manager, which removes the content types and contents beneath the page itself. The
        // database's cascading foreign keys do not do it: these entities are soft-deleted, so a delete is
        // an UPDATE and ON DELETE CASCADE never fires (see PageManager.DeleteAsync). PageManager.DeleteAsync
        // re-queries the content types itself rather than reading this Page's navigation collection, so
        // includeDetails stays false here.
        var page = await PageRepository.GetAsync(id, includeDetails: false);

        await PageManager.DeleteAsync(page);
    }

    /// <summary>
    /// Maps a page fetched with <c>includeDetails: true</c>, so <see cref="Page.ContentTypes"/> is already
    /// loaded and nesting it costs no extra query (总体设计 §2.3).
    /// </summary>
    protected virtual PageDto MapToDto(Page page)
    {
        var dto = ObjectMapper.Map<Page, PageDto>(page);
        dto.ContentTypes = page.ContentTypes.Select(MapContentType).ToList();
        return dto;
    }

    protected virtual ContentTypeDto MapContentType(ContentType contentType)
    {
        var dto = ObjectMapper.Map<ContentType, ContentTypeDto>(contentType);
        dto.Fields = contentType.Fields.ToDtoList();
        return dto;
    }
}
