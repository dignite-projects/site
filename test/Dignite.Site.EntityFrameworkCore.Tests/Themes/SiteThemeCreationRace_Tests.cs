using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Themes;

/// <summary>
/// Two requests that both find no theme and both create one. The second request is played by making the next read of
/// the theme miss, after the first one has created it: its insert then meets the unique index the way a request
/// running at the same moment does. The tests run in a tenant, since the SQLite database here treats NULLs as distinct
/// and does not stop a second host row (see <see cref="SiteThemeIndexes_Tests"/> for SQL Server).
/// </summary>
public class SiteThemeCreationRace_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteThemeManager _manager;
    private readonly ISiteThemeRepository _themeRepository;
    private readonly ISiteThemeFileRepository _fileRepository;
    private readonly ThemeRepositoryFaults _faults;
    private readonly ICurrentTenant _currentTenant;

    public SiteThemeCreationRace_Tests()
    {
        _manager = GetRequiredService<SiteThemeManager>();
        _themeRepository = GetRequiredService<ISiteThemeRepository>();
        _fileRepository = GetRequiredService<ISiteThemeFileRepository>();
        _faults = GetRequiredService<ThemeRepositoryFaults>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.AddSingleton<ThemeRepositoryFaults>();
        services.Replace(ServiceDescriptor.Transient<ISiteThemeRepository, FaultySiteThemeRepository>());
    }

    [Fact]
    public async Task The_Second_Request_Should_Get_The_Theme_The_First_One_Created()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var first = await WithUnitOfWorkAsync(() => _manager.GetOrCreateAsync());

            _faults.MissNextRead = true;
            var second = await WithUnitOfWorkAsync(async () =>
            {
                var theme = await _manager.GetOrCreateAsync();

                // The refused insert was not this unit of work's: it saves the rest of the request as usual.
                await _manager.SaveFileAsync(theme, "Events", "<p>x</p>");
                await _manager.SetDefaultLayoutAsync(theme, null);
                return theme;
            });

            _faults.MissNextRead.ShouldBeFalse();
            second.Id.ShouldBe(first.Id);
            (await WithUnitOfWorkAsync(() => _themeRepository.GetCountAsync())).ShouldBe(1);

            var files = await WithUnitOfWorkAsync(() => _fileRepository.GetListAsync(first.Id));
            files.Select(f => f.Path).ShouldBe(new[] { "Events", "css/site.css", "layouts/default" });
            (await WithUnitOfWorkAsync(() => _themeRepository.GetAsync(first.Id))).DefaultLayout.ShouldBeNull();
        }
    }

    [Fact]
    public async Task A_Created_Theme_Should_Belong_To_The_Callers_Unit_Of_Work()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            var theme = await WithUnitOfWorkAsync(async () =>
            {
                var created = await _manager.GetOrCreateAsync();
                await _manager.SetDefaultLayoutAsync(created, null);
                return created;
            });

            (await WithUnitOfWorkAsync(() => _themeRepository.GetAsync(theme.Id))).DefaultLayout.ShouldBeNull();
            (await WithUnitOfWorkAsync(() => _fileRepository.GetListAsync(theme.Id))).Count.ShouldBe(2);
        }
    }

    /// <summary>A failure that leaves no theme behind is not another request's theme: it reaches the caller.</summary>
    [Fact]
    public async Task A_Failure_That_Leaves_No_Theme_Should_Be_Rethrown()
    {
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            _faults.FailNextInsert = true;

            var exception = await Should.ThrowAsync<InvalidOperationException>(
                () => WithUnitOfWorkAsync(() => _manager.GetOrCreateAsync()));

            exception.Message.ShouldBe(FaultySiteThemeRepository.InsertFailure);
            (await WithUnitOfWorkAsync(() => _themeRepository.GetCountAsync())).ShouldBe(0);
        }
    }

    /// <summary>
    /// Where the database does let a second host theme in, the administration API and the site must read the same
    /// one, even when the two were made in the same instant: the reads order by creation time and then by id.
    /// </summary>
    [Fact]
    public async Task Two_Host_Themes_Made_At_Once_Should_Read_As_The_Same_One()
    {
        var later = new SiteTheme(Guid.Parse("ffffffff-0000-0000-0000-000000000000"));
        later.SetLiveRelease(Guid.NewGuid());
        var earlier = new SiteTheme(Guid.Parse("00000000-0000-0000-0000-00000000ffff"));
        earlier.SetLiveRelease(Guid.NewGuid());

        await WithUnitOfWorkAsync(async () =>
        {
            // Inserted in the opposite order to their ids, so that a read that stops at the creation time - which
            // they share - would find the other one.
            await _themeRepository.InsertAsync(later, autoSave: true);
            await _themeRepository.InsertAsync(earlier, autoSave: true);

            var dbContext = await GetRequiredService<IDbContextProvider<ISiteDbContext>>().GetDbContextAsync();
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE SiteThemes SET CreationTime = {0}", new DateTime(2026, 11, 5, 15, 22, 11));
        });

        var current = await WithUnitOfWorkAsync(() => _themeRepository.FindCurrentAsync());
        var liveReleaseId = await WithUnitOfWorkAsync(() => _themeRepository.FindLiveReleaseIdAsync());

        current.ShouldNotBeNull().Id.ShouldBe(earlier.Id);
        liveReleaseId.ShouldBe(earlier.LiveReleaseId);
    }
}

public class ThemeRepositoryFaults
{
    /// <summary>The next <see cref="ISiteThemeRepository.FindCurrentAsync"/> finds nothing, as for a request that read before another one inserted.</summary>
    public bool MissNextRead { get; set; }

    /// <summary>The next insert of a theme fails before it reaches the database.</summary>
    public bool FailNextInsert { get; set; }
}

/// <summary>Registered by <see cref="SiteThemeCreationRace_Tests"/> only: a repository is otherwise picked up for every test.</summary>
[DisableConventionalRegistration]
public class FaultySiteThemeRepository : EfCoreSiteThemeRepository
{
    public const string InsertFailure = "The database is not there.";

    private readonly ThemeRepositoryFaults _faults;

    public FaultySiteThemeRepository(IDbContextProvider<ISiteDbContext> dbContextProvider, ThemeRepositoryFaults faults)
        : base(dbContextProvider)
    {
        _faults = faults;
    }

    public override async Task<SiteTheme?> FindCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (_faults.MissNextRead)
        {
            _faults.MissNextRead = false;
            return null;
        }

        return await base.FindCurrentAsync(cancellationToken);
    }

    public override async Task<SiteTheme> InsertAsync(SiteTheme entity, bool autoSave = false, CancellationToken cancellationToken = default)
    {
        if (_faults.FailNextInsert)
        {
            _faults.FailNextInsert = false;
            throw new InvalidOperationException(InsertFailure);
        }

        return await base.InsertAsync(entity, autoSave, cancellationToken);
    }
}
