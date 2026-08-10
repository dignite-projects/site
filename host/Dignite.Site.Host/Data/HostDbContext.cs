using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.EntityFrameworkCore;
using Dignite.FileExplorer.Files;
using Dignite.Site.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace Dignite.Site.Host.Data;

/// <summary>
/// The host's single physical database. <see cref="ReplaceDbContextAttribute"/> is what makes
/// <c>IDbContextProvider&lt;ISiteDbContext&gt;</c> resolve to this class, so the Site module's
/// repositories - and FlexFields' index manager and query executor, which are generic over
/// <c>ISiteDbContext</c> - all share this context and therefore one unit of work. Without it, the
/// derived index would be written through a different DbContext instance than the content it was
/// derived from, and EF Core cannot compose a query across two.
///
/// <para>
/// <see cref="IFileExplorerDbContext"/> is replaced the same way (GitHub issue #41's follow-up), so
/// Dignite.FileExplorer's tables land in this same physical database rather than a separate one -
/// FileExplorerDbContext's own <c>[ConnectionStringName]</c> is moot once replaced, the same way
/// SiteDbContext's would be. The model itself is not configured here, though: FileExplorer is part of
/// Site (same as FlexFields), so <c>ConfigureFileExplorer()</c> lives in
/// <c>SiteDbContextModelCreatingExtensions.ConfigureSite()</c> alongside Site's own entities - this
/// class only supplies the concrete DbSets and the interface implementation the replace attribute
/// requires, the same division <see cref="ISiteDbContext"/> already has between here and
/// <c>SiteDbContext</c>.
/// </para>
/// </summary>
[ReplaceDbContext(typeof(ISiteDbContext))]
[ReplaceDbContext(typeof(IFileExplorerDbContext))]
public class HostDbContext : AbpDbContext<HostDbContext>, ISiteDbContext, IFileExplorerDbContext
{
    public DbSet<Dignite.Site.Pages.Page> Pages { get; set; } = default!;

    public DbSet<Dignite.Site.ContentTypes.ContentType> ContentTypes { get; set; } = default!;

    public DbSet<Dignite.Site.Fields.Field> Fields { get; set; } = default!;


    public DbSet<Dignite.Site.Contents.Content> Contents { get; set; } = default!;

    public DbSet<Dignite.Site.Contents.ContentFlexFieldIndex> ContentFlexFieldIndexes { get; set; } = default!;

    public DbSet<DirectoryDescriptor> DirectoryDescriptors { get; set; } = default!;

    public DbSet<FileDescriptor> FileDescriptors { get; set; } = default!;


    public const string DbTablePrefix = "App";
    public const string DbSchema = null;

    public HostDbContext(DbContextOptions<HostDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigurePermissionManagement();
        builder.ConfigureBlobStoring();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureTenantManagement();

        /* Configure your own entities here */

        // Configures FileExplorer's entities too - see the class doc comment.
        builder.ConfigureSite();
    }
}

