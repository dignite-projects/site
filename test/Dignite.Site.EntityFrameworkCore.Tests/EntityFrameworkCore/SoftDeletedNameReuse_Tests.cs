using System;
using System.Threading.Tasks;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.EntityFrameworkCore;

/// <summary>
/// Every entity here is soft-deleted, so a delete leaves its row behind. The manager's duplicate check
/// goes through the repository and cannot see that row, which means the unique index has to ignore it
/// too - otherwise a name freed by a delete passes the manager and is then rejected by the database.
/// <para>
/// Every test runs inside a tenant: SQL treats NULLs as distinct in a unique index, so a host row
/// (TenantId NULL) never collides at the database level and would pass whether or not the index is
/// filtered.
/// </para>
/// </summary>
public class SoftDeletedNameReuse_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly PageManager _pageManager;
    private readonly IPageRepository _pageRepository;
    private readonly ContentTypeManager _contentTypeManager;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly FieldManager _fieldManager;
    private readonly ContentManager _contentManager;
    private readonly IContentRepository _contentRepository;
    private readonly ICurrentTenant _currentTenant;

    public SoftDeletedNameReuse_Tests()
    {
        _pageManager = GetRequiredService<PageManager>();
        _pageRepository = GetRequiredService<IPageRepository>();
        _contentTypeManager = GetRequiredService<ContentTypeManager>();
        _contentTypeRepository = GetRequiredService<IContentTypeRepository>();
        _fieldManager = GetRequiredService<FieldManager>();
        _contentManager = GetRequiredService<ContentManager>();
        _contentRepository = GetRequiredService<IContentRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Should_Reuse_The_Name_And_Route_Of_A_Deleted_Page()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var first = await WithUnitOfWorkAsync(() =>
                _pageManager.CreateAsync("reuse-page", "Reuse page", "/reuse-page"));

            await WithUnitOfWorkAsync(async () =>
                await _pageManager.DeleteAsync(await _pageRepository.GetAsync(first.Id)));

            var second = await WithUnitOfWorkAsync(() =>
                _pageManager.CreateAsync("reuse-page", "Reuse page", "/reuse-page"));

            second.Id.ShouldNotBe(first.Id);
        }
    }

    [Fact]
    public async Task Should_Reuse_The_Name_Of_A_Deleted_Content_Type()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var page = await WithUnitOfWorkAsync(() =>
                _pageManager.CreateAsync("reuse-type-page", "Page", "/reuse-type-page"));

            var first = await WithUnitOfWorkAsync(() =>
                _contentTypeManager.CreateAsync(page.Id, "reuse-type", "Reuse type"));

            await WithUnitOfWorkAsync(async () => await _contentTypeRepository.DeleteAsync(first.Id));

            var second = await WithUnitOfWorkAsync(() =>
                _contentTypeManager.CreateAsync(page.Id, "reuse-type", "Reuse type"));

            second.Id.ShouldNotBe(first.Id);
        }
    }

    [Fact]
    public async Task Should_Reuse_The_Name_Of_A_Deleted_Field()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var first = await WithUnitOfWorkAsync(() =>
                _fieldManager.CreateAsync("reuse-field", "Reuse field", "Text"));

            await WithUnitOfWorkAsync(() => _fieldManager.DeleteAsync(first));

            var second = await WithUnitOfWorkAsync(() =>
                _fieldManager.CreateAsync("reuse-field", "Reuse field", "Text"));

            second.Id.ShouldNotBe(first.Id);
        }
    }

    [Fact]
    public async Task Should_Reuse_The_Slug_Of_A_Deleted_Content()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var page = await WithUnitOfWorkAsync(() =>
                _pageManager.CreateAsync("reuse-slug-page", "Page", "/reuse-slug-page/{slug}"));
            var type = await WithUnitOfWorkAsync(() =>
                _contentTypeManager.CreateAsync(page.Id, "reuse-slug-type", "Type"));

            var first = await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
                type.Id, "en", "reuse-slug", SiteTestData.PublishTime));

            await WithUnitOfWorkAsync(async () => await _contentRepository.DeleteAsync(first.Id));

            var second = await WithUnitOfWorkAsync(() => _contentManager.CreateAsync(
                type.Id, "en", "reuse-slug", SiteTestData.PublishTime));

            second.Id.ShouldNotBe(first.Id);
        }
    }
}
