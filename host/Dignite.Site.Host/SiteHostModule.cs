using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Dignite.Site.Host.Data;
using Dignite.Site.Host.Localization;
using Dignite.Site.Host.Menus;
using Dignite.Site.Host.Permissions;
using Dignite.Site.Host.HealthChecks;
using OpenIddict.Validation.AspNetCore;
using System;
using Volo.Abp;
using Volo.Abp.Studio;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Toolbars;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Mapperly;
using Volo.Abp.Caching;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Identity.Web;
using Volo.Abp.Uow;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.Web;
using Volo.Abp.Emailing;
using Volo.Abp.Localization;
using Volo.Abp.Localization.ExceptionHandling;
using Localization.Resources.AbpUi;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.HttpApi;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.PermissionManagement.Web;
using Volo.Abp.SettingManagement;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.SettingManagement.Web;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.Validation.Localization;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.Security.Claims;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.TenantManagement.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Dignite.Abp.FileStoring;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Files;
using Dignite.Site.Mcp;
using Dignite.Site.Public;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;

using Microsoft.Extensions.Hosting;

namespace Dignite.Site.Host;

[DependsOn(
    // ABP Framework packages
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpAutofacModule),
    typeof(AbpMapperlyModule),
    typeof(AbpCachingModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpStudioClientAspNetCoreModule),

    // lepton-theme
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),

    // Account module packages
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpAccountHttpApiModule),
    typeof(AbpAccountApplicationModule),
        
    // Tenant Management module packages
    typeof(AbpTenantManagementWebModule),
    typeof(AbpTenantManagementHttpApiModule),
    typeof(AbpTenantManagementApplicationModule),

    // Identity module packages
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpPermissionManagementDomainOpenIddictModule),
    typeof(AbpIdentityWebModule),
    typeof(AbpIdentityHttpApiModule),
    typeof(AbpIdentityApplicationModule),

    // Permission Management module packages
    typeof(AbpPermissionManagementWebModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpPermissionManagementHttpApiModule),

    // Feature Management module packages
    typeof(AbpFeatureManagementWebModule),
    typeof(AbpFeatureManagementHttpApiModule),
    typeof(AbpFeatureManagementApplicationModule),

    // Setting Management module packages
    typeof(AbpSettingManagementWebModule),
    typeof(AbpSettingManagementHttpApiModule),
    typeof(AbpSettingManagementApplicationModule),

    // Entity Framework Core packages for the used modules
    typeof(AbpAuditLoggingEntityFrameworkCoreModule),
    typeof(AbpFeatureManagementEntityFrameworkCoreModule),
    typeof(AbpIdentityEntityFrameworkCoreModule),
    typeof(AbpOpenIddictEntityFrameworkCoreModule),
    typeof(AbpTenantManagementEntityFrameworkCoreModule),
    typeof(AbpPermissionManagementEntityFrameworkCoreModule),
    typeof(AbpSettingManagementEntityFrameworkCoreModule),
    typeof(AbpBackgroundJobsEntityFrameworkCoreModule),
    typeof(BlobStoringDatabaseEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),

    // The active BLOB storage provider (GitHub issue #41) - filesystem for dev, production provider
    // still open. ConfigureBlobStoring below is what actually points SiteFileContainerNames.Default at
    // it; this DependsOn is what makes that provider resolvable at all.
    typeof(AbpBlobStoringFileSystemModule),

    // The Site content kernel - EF Core for SiteHostDbContext's ISiteDbContext replacement (see
    // SiteHostDbContext), the unified Application module so the concrete app service classes are registered,
    // and the unified HttpApi module for the explicit Admin/Public controllers.
    typeof(SiteEntityFrameworkCoreModule),
    typeof(SiteApplicationModule),
    typeof(SiteHttpApiModule),

    // The site-facing web layer: robots.txt, sitemap.xml and the per-page feeds, which are documents on
    // the tenant's own domain rather than API endpoints. Also where ISiteBaseUrlResolver gains its
    // fall-back-to-the-request behaviour, so a tenant with no primary domain configured still serves
    // working absolute URLs.
    typeof(SitePublicWebModule),

    // The MCP endpoint (总体设计 §6.1). Loaded here rather than inside SiteApplicationModule because it
    // maps an HTTP endpoint - it needs a host with a pipeline, and it needs to sit behind the same
    // authentication, multi-tenancy and unit-of-work middleware everything else here does.
    typeof(SiteMcpModule)

    // Dignite.FileExplorer's own Application + HttpApi (GitHub issue #41's follow-up) reach this Host
    // transitively through SiteApplicationModule/SiteHttpApiModule -> Admin/Public -> CommonApplication/
    // CommonHttpApi, the same as FlexFields does - no direct dependency needed here.
)]
public class SiteHostModule : AbpModule
{
    /* Single point to enable/disable multi-tenancy */
    public const bool IsMultiTenant = true;

    private const string DefaultCorsPolicyName = "Default";

    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(SiteHostResource)
            );
        });

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("Host");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        if (!hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx", configuration["AuthServer:CertificatePassPhrase"]!);
            });
        }

        // PreConfigure, not Configure: SiteMcpModule reads these while building the service collection.
        PreConfigure<SiteMcpOptions>(options =>
        {
            // Pin the MCP endpoint to the MCP scheme, which forwards authentication to OpenIddict (see
            // ConfigureMcpResourceMetadata) and answers a challenge with 401 plus the RFC 9728
            // `resource_metadata` header. Leaving it on the application's default policy instead would
            // hand the challenge to the Identity application cookie handler, which answers with a 302 to
            // /Account/Login - a redirect no MCP client can act on.
            options.AuthenticationSchemes.Add(McpAuthenticationDefaults.AuthenticationScheme);
        });

        SiteHostGlobalFeatureConfigurator.Configure();
        SiteHostModuleExtensionConfigurator.Configure();
        SiteHostEfCoreEntityExtensionMappings.Configure();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        if (hostingEnvironment.IsDevelopment())
        {
            context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());

            context.Services.AddRazorPages()
                .AddRazorRuntimeCompilation();
        }

        ConfigureStudio(hostingEnvironment);
        ConfigureAuthentication(context);
        ConfigureMultiTenancy();
        ConfigureUrls(configuration);
        ConfigureCors(context, configuration);
        ConfigureBundles(hostingEnvironment);
        ConfigureHealthChecks(context);
        ConfigureSwagger(context.Services);
        ConfigureAutoApiControllers();
        ConfigureVirtualFiles(hostingEnvironment);
        ConfigureLocalization();
        ConfigureNavigationServices();
        ConfigureEfCore(context);
        ConfigureBlobStoring(hostingEnvironment);

        Configure<RazorPagesOptions>(options =>
        {
        });
    }

    /// <summary>
    /// The backend GitHub issue #41 stood up for Dignite.FileExplorer: filesystem provider for dev, under
    /// the container name #42's FileExplorer field type points its FileContainerName at. Production
    /// provider (Azure/S3/other) is still an open decision (#41) - swapping it later only touches this
    /// method, since FileDescriptorManager/DirectoryManager address blobs by container name, never by
    /// provider.
    /// </summary>
    private void ConfigureBlobStoring(IHostEnvironment hostingEnvironment)
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure(SiteFileContainerNames.Default, container =>
            {
                container.UseFileSystem(fileSystem =>
                {
                    fileSystem.BasePath = Path.Combine(hostingEnvironment.ContentRootPath, "App_Data", "files");
                });

                // CreateFilePermissionName only, reusing SiteAdminPermissions.Contents.Create rather than
                // FileExplorerPermissions.Files.Management - an upload is how a content's images get here,
                // so whoever may create content may upload, with no MCP-specific permission invented
                // (总体设计 §6.2.5). GetFilePermissionName is deliberately left unset: unset means
                // unauthenticated reads (FileDescriptorAuthorizationHandler's own default), which is correct
                // for this container - a published content's images must load for anonymous site visitors,
                // not just authenticated editors.
                container.SetAuthorizationConfiguration(config =>
                {
                    config.CreateFilePermissionName = SiteAdminPermissions.Contents.Create;
                });
            });
        });
    }

    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddHostHealthChecks();
    }

    private void ConfigureStudio(IHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsProduction())
        {
            Configure<AbpStudioClientOptions>(options =>
            {
                options.IsLinkEnabled = false;
            });
        }
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });

        ConfigureMcpResourceMetadata(context);
    }

    /// <summary>
    /// Publishes <c>/.well-known/oauth-protected-resource</c> (RFC 9728) and points the MCP endpoint's
    /// 401 challenge at it.
    /// <para>
    /// This is what turns "the MCP endpoint accepts our bearer tokens" into "an MCP client can actually
    /// connect": the client is not a service holding a pre-issued key, it discovers where to authenticate
    /// by reading this document off the resource it was pointed at. Tokens themselves are unchanged - same
    /// OpenIddict server, same scopes, same permission claims (总体设计 §6.2.5).
    /// </para>
    /// <para>
    /// Clients then use the pre-registered <c>Site_Mcp</c> client id (see <see cref="SiteHostConsts"/>), which
    /// is the MCP specification's own first-priority registration mechanism. Dynamic Client Registration
    /// is deprecated as of the <c>2026-07-28</c> revision and is not enabled here; its successor, Client
    /// ID Metadata Documents, is an authorization-server capability and would be an OpenIddict change.
    /// </para>
    /// </summary>
    private void ConfigureMcpResourceMetadata(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        var selfUrl = configuration["App:SelfUrl"]?.TrimEnd('/');

        // Falls back when the key is present but blank, not only when it is missing. `?.` short-circuits
        // on null alone, so an empty environment variable - AuthServer__Authority= from an unset Helm
        // value, or a bare "/" that TrimEnd reduces to nothing - would otherwise defeat the fallback and
        // abort startup blaming App:SelfUrl, which is correctly set.
        var configuredAuthority = configuration["AuthServer:Authority"]?.TrimEnd('/');
        var authority = configuredAuthority.IsNullOrWhiteSpace() ? selfUrl : configuredAuthority;

        // Fails at startup rather than degrading. Neither half of this is optional once the MCP module is
        // loaded: the endpoint is pinned to the MCP scheme, so not registering it would leave every
        // request demanding a handler that does not exist; and the SDK does NOT synthesize a metadata
        // document from the request when ResourceMetadata is left unset - it throws out of
        // UseAuthentication(), which turns an anonymous GET of the public
        // /.well-known/oauth-protected-resource path into a 500. A misconfigured deployment should hear
        // about it here, not from an AI client that cannot discover where to authenticate.
        if (selfUrl.IsNullOrWhiteSpace() || authority.IsNullOrWhiteSpace())
        {
            throw new AbpException(
                "App:SelfUrl must be configured. The MCP endpoint publishes RFC 9728 protected-resource "
                + "metadata describing its own origin, and cannot derive one. Set App:SelfUrl (and "
                + "optionally AuthServer:Authority, which defaults to it).");
        }

        context.Services.AddAuthentication()
            .AddMcp(options =>
            {
                // The MCP scheme authenticates nothing itself - it forwards, and its default target is a
                // scheme literally named "Bearer" (what the SDK's own JwtBearer samples register). This
                // host has no such scheme, so without redirecting it here every MCP request fails with
                // "No authentication handler is registered for the scheme 'Bearer'".
                options.ForwardAuthenticate = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;

                options.ResourceMetadata = new ProtectedResourceMetadata
                {
                    Resource = selfUrl!,
                    AuthorizationServers = { authority! },
                    ScopesSupported = { SiteHostConsts.ApiScopeName }
                };
            });
    }

    private void ConfigureMultiTenancy()
    {
        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = IsMultiTenant;
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
        });
    }

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddPolicy(DefaultCorsPolicyName, builder =>
            {
                builder
                    .WithOrigins(
                        (configuration["App:CorsOrigins"] ?? "")
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim().RemovePostFix("/"))
                            .ToArray()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    private void ConfigureBundles(IHostEnvironment hostingEnvironment)
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );

            options.ScriptBundles.Configure(
                LeptonXLiteThemeBundles.Scripts.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-scripts.js");
                    if (hostingEnvironment.IsDevelopment())
                    {
                        bundle.AddFiles("/dev-login-helper.js");
                    }
                }
            );
        });
    }

    private void ConfigureLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<SiteHostResource>("en")
                .AddBaseTypes(typeof(AbpValidationResource), typeof(AbpUiResource))
                .AddVirtualJson("/Localization/Host");

            options.DefaultResourceType = typeof(SiteHostResource);
            
            options.Languages.Add(new LanguageInfo("en", "en", "English")); 
            options.Languages.Add(new LanguageInfo("ar", "ar", "Arabic")); 
            options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "Chinese (Simplified)")); 
            options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "Chinese (Traditional)")); 
            options.Languages.Add(new LanguageInfo("cs", "cs", "Czech")); 
            options.Languages.Add(new LanguageInfo("en-GB", "en-GB", "English (UK)")); 
            options.Languages.Add(new LanguageInfo("fi", "fi", "Finnish")); 
            options.Languages.Add(new LanguageInfo("fr", "fr", "French")); 
            options.Languages.Add(new LanguageInfo("de-DE", "de-DE", "German (Germany)")); 
            options.Languages.Add(new LanguageInfo("hi", "hi", "Hindi ")); 
            options.Languages.Add(new LanguageInfo("hu", "hu", "Hungarian")); 
            options.Languages.Add(new LanguageInfo("is", "is", "Icelandic")); 
            options.Languages.Add(new LanguageInfo("it", "it", "Italian")); 
            options.Languages.Add(new LanguageInfo("pt-BR", "pt-BR", "Portuguese (Brazil)")); 
            options.Languages.Add(new LanguageInfo("ro-RO", "ro-RO", "Romanian (Romania)")); 
            options.Languages.Add(new LanguageInfo("ru", "ru", "Russian")); 
            options.Languages.Add(new LanguageInfo("sk", "sk", "Slovak")); 
            options.Languages.Add(new LanguageInfo("es", "es", "Spanish")); 
            options.Languages.Add(new LanguageInfo("sv", "sv", "Swedish")); 
            options.Languages.Add(new LanguageInfo("tr", "tr", "Turkish")); 
            options.Languages.Add(new LanguageInfo("ja", "ja", "日语")); 

        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace("Host", typeof(SiteHostResource));
        });
    }

    private void ConfigureVirtualFiles(IWebHostEnvironment hostingEnvironment)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SiteHostModule>();
            if (hostingEnvironment.IsDevelopment())
            {
                /* Using physical files in development, so we don't need to recompile on changes */
                options.FileSets.ReplaceEmbeddedByPhysical<SiteHostModule>(hostingEnvironment.ContentRootPath);
            }
        });
    }

    private void ConfigureAutoApiControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(SiteHostModule).Assembly);
        });
    }

    private void ConfigureSwagger(IServiceCollection services)
    {
        services.AddAbpSwaggerGen(
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Host API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            }
        );
    }

    private void ConfigureNavigationServices()
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new SiteHostMenuContributor());
        });

        Configure<AbpToolbarOptions>(options =>
        {
            options.Contributors.Add(new SiteHostToolbarContributor());
        });
    }
    
    private void ConfigureEfCore(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<SiteHostDbContext>(options =>
        {
            /* You can remove "includeAllEntities: true" to create
             * default repositories only for aggregate roots
             * Documentation: https://docs.abp.io/en/abp/latest/Entity-Framework-Core#add-default-repositories
             */
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(configurationContext =>
            {
                configurationContext.UseSqlite();
            });
        });
        
        context.Services.AddAlwaysDisableUnitOfWorkTransaction();
        Configure<AbpUnitOfWorkDefaultOptions>(options =>
        {
            options.TransactionBehavior = UnitOfWorkTransactionBehavior.Disabled;
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
        }

        app.UseCorrelationId();
        app.MapAbpStaticAssets();
        app.UseRouting();
        app.UseCors(DefaultCorsPolicyName);
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (IsMultiTenant)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();

        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Host API");
        });

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }
}
