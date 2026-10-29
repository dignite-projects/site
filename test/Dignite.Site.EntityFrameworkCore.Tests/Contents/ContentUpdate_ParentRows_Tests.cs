using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.Admin.Contents;
using Dignite.Site.Admin.Pages;
using Dignite.Site.ContentTypes;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Contents;
using Dignite.Site.Pages;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Uow;
using Xunit;

namespace Dignite.Site.Contents;

/// <summary>
/// GitHub issue #71, second defect: updating a content must write that content's row and nothing above it.
/// <para>
/// A content's page and content type are read while it is updated (the manager resolves the type, and an
/// MCP tool resolves the page by name), but neither is changed by it. Writing them anyway bumps their
/// <c>ConcurrencyStamp</c>, so two saves of two <i>different</i> contents under one page conflict on the
/// shared parent row - which is how three parallel <c>site_update_content</c> calls ended up with one of
/// them rolled back, and how two editors saving two posts of one type at once get a 409 for no visible
/// reason.
/// </para>
/// <para>
/// The cause was never in Site's model: ABP before 10.7.0 treated any change to a dependent as a change of
/// its tracked principal, even when the principal has no navigation to it - and <c>Content</c> reaches its
/// page and its content type through a bare foreign key (<c>HasOne&lt;T&gt;().WithMany()</c>). The fix is
/// ABP's (abpframework/abp@743cd7ac3, in 10.7.0); these tests are what stops Site from silently losing it
/// to an ABP downgrade, or to a navigation added to the model later.
/// </para>
/// </summary>
public class ContentUpdate_ParentRows_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ContentManager _contentManager;
    private readonly IContentRepository _contentRepository;
    private readonly IContentAdminAppService _contentAppService;
    private readonly ContentTools _contentTools;
    private readonly IPageRepository _pageRepository;
    private readonly IContentTypeRepository _contentTypeRepository;

    public ContentUpdate_ParentRows_Tests()
    {
        _contentManager = GetRequiredService<ContentManager>();
        _contentRepository = GetRequiredService<IContentRepository>();
        _contentAppService = GetRequiredService<IContentAdminAppService>();
        _contentTools = GetRequiredService<ContentTools>();
        _pageRepository = GetRequiredService<IPageRepository>();
        _contentTypeRepository = GetRequiredService<IContentTypeRepository>();
    }

    /// <summary>The domain write path on its own: one unit of work, one content, slug untouched.</summary>
    [Fact]
    public async Task Should_Leave_The_Page_And_Content_Type_Rows_Alone_When_The_Manager_Updates_A_Content()
    {
        var before = await ReadParentRowsAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            var content = (await _contentRepository.FindBySlugAsync(
                SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug))!;

            await _contentManager.UpdateAsync(
                content, content.Slug, content.PublishTime, content.Status,
                new Dictionary<string, object?> { ["title"] = "My trip, revised", ["views"] = 43 });
        });

        await ShouldHaveWrittenTheContentAsync(SiteTestData.TripSlug, "My trip, revised");
        (await ReadParentRowsAsync()).ShouldBe(before);
    }

    /// <summary>
    /// The admin UI's path: the application service loads the content and hands it to the manager, each
    /// call in a unit of work of its own - so the content type is tracked, and the page is not.
    /// </summary>
    [Fact]
    public async Task Should_Leave_The_Page_And_Content_Type_Rows_Alone_When_The_Admin_Service_Updates_A_Content()
    {
        var before = await ReadParentRowsAsync();

        var trip = await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, SiteTestData.TripSlug));

        await _contentAppService.UpdateAsync(trip!.Id, new UpdateContentDto
        {
            Slug = trip.Slug,
            PublishTime = trip.PublishTime,
            Status = trip.Status,
            FieldValues = new Dictionary<string, object?> { ["title"] = "My trip, revised", ["views"] = 43 }
        });

        await ShouldHaveWrittenTheContentAsync(SiteTestData.TripSlug, "My trip, revised");
        (await ReadParentRowsAsync()).ShouldBe(before);
    }

    /// <summary>
    /// The MCP path, which is the one the issue was reported on. An MCP request runs inside a single unit of
    /// work, and its tool resolves the page by name first (with its content types) before it updates - so,
    /// unlike the admin service above, the page and every content type of it are already tracked by the
    /// time the content is saved.
    /// </summary>
    [Fact]
    public async Task Should_Leave_The_Page_And_Content_Type_Rows_Alone_When_An_Mcp_Tool_Updates_A_Content()
    {
        var before = await ReadParentRowsAsync();

        await WithUnitOfWorkAsync(() => _contentTools.UpdateContentAsync(
            page: "blog",
            cultureName: SiteTestData.EnglishCulture,
            slug: SiteTestData.TripSlug,
            fieldValues: new Dictionary<string, object?> { ["title"] = "My trip, revised", ["views"] = 43 }));

        await ShouldHaveWrittenTheContentAsync(SiteTestData.TripSlug, "My trip, revised");
        (await ReadParentRowsAsync()).ShouldBe(before);
    }

    /// <summary>
    /// What the rows above cost, seen from the outside: the issue's reproduction, made deterministic.
    /// Parallel <c>site_update_content</c> calls (or two editors saving at once) look like this to the
    /// database: the slower request has already read the page and its content types when the faster one
    /// runs start to finish, and then carries on with the concurrency stamps it read. The two share nothing
    /// but a page (and, in the first case, a content type) that neither of them changes, so the slower one
    /// has to commit - when the parents were being rewritten, it failed on their stamps instead.
    /// </summary>
    [Theory]
    // post-article and post-article: they share the page and the content type.
    [InlineData(SiteTestData.TripSlug, SiteTestData.DraftSlug)]
    // post-article and post-gallery: they share the page only.
    [InlineData(SiteTestData.TripSlug, SiteTestData.GallerySlug)]
    public async Task Should_Let_A_Slower_Update_Commit_After_A_Faster_One_Of_Another_Content_Under_The_Same_Page(
        string fasterSlug,
        string slowerSlug)
    {
        // What has to overlap is the slower request's read of the parents and its write - not two units of
        // work opened back to back: a content update is flushed in the middle of the tool call (the kernel's
        // index synchronization saves the context), so a request that starts after another one has run its
        // tool reads the parents' new stamps and could never conflict, whatever the parents' rows are.
        // The two requests take turns and never touch the database at the same moment: the test's
        // connection is one in-memory SQLite connection, shared by every unit of work.
        var slowerHasReadThePage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fasterIsDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var slower = Task.Run(async () =>
        {
            using var scope = ServiceProvider.CreateScope();
            using var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>()
                .Begin(new AbpUnitOfWorkOptions());

            // What every site_* tool does first: resolve the page by name, which tracks it together with its
            // content types, at the stamps they have right now.
            await scope.ServiceProvider.GetRequiredService<IPageAdminAppService>().FindByNameAsync("blog");

            slowerHasReadThePage.SetResult();
            await fasterIsDone.Task;

            await scope.ServiceProvider.GetRequiredService<ContentTools>().UpdateContentAsync(
                page: "blog",
                cultureName: SiteTestData.EnglishCulture,
                slug: slowerSlug,
                fieldValues: new Dictionary<string, object?> { ["title"] = "Written by the slower request" });

            await unitOfWork.CompleteAsync();
        });

        if (await Task.WhenAny(slowerHasReadThePage.Task, slower) == slower)
        {
            // It ended before it got to wait: surface why, instead of waiting for it forever.
            await slower;
        }

        try
        {
            await WithUnitOfWorkAsync(() => _contentTools.UpdateContentAsync(
                page: "blog",
                cultureName: SiteTestData.EnglishCulture,
                slug: fasterSlug,
                fieldValues: new Dictionary<string, object?> { ["title"] = "Written by the faster request" }));
        }
        finally
        {
            // Also when the faster request failed: the slower one is parked on this, and would hang the run.
            fasterIsDone.SetResult();
        }

        await Should.NotThrowAsync(() => slower);

        await ShouldHaveWrittenTheContentAsync(fasterSlug, "Written by the faster request");
        await ShouldHaveWrittenTheContentAsync(slowerSlug, "Written by the slower request");
    }

    /// <summary>The content did change - so a pass above is "nothing else was written", not "nothing was".</summary>
    private async Task ShouldHaveWrittenTheContentAsync(string slug, string expectedTitle)
    {
        var content = (await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, slug)))!;

        content.GetField(SiteTestData.TitleFieldName).ShouldBe(expectedTitle);
        content.LastModificationTime.ShouldNotBeNull();
    }

    private Task<ParentRows> ReadParentRowsAsync()
    {
        return WithUnitOfWorkAsync(async () =>
        {
            var page = await _pageRepository.GetAsync(SiteTestData.BlogPageId);
            var contentType = await _contentTypeRepository.GetAsync(SiteTestData.PostArticleTypeId);

            return new ParentRows(
                new RowStamp(page.ConcurrencyStamp, page.LastModificationTime),
                new RowStamp(contentType.ConcurrencyStamp, contentType.LastModificationTime));
        });
    }

    /// <summary>What the optimistic-concurrency check and the audit columns record about one row's last write.</summary>
    private sealed record RowStamp(string? ConcurrencyStamp, DateTime? LastModificationTime);

    private sealed record ParentRows(RowStamp Page, RowStamp ContentType);
}
