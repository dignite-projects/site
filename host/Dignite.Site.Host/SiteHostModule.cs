using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Dignite.Site.Host.Data;
using Dignite.Site.Host.Localization;
using Dignite.Site.Host.Menus;
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
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Files;
using Dignite.Site.Mcp;
using Dignite.Site.Public;
using Dignite.Abp.AspNetCore.Mcp;
using Dignite.Abp.BlobManagement;
using Dignite.Abp.BlobManagement.Presets;
using Dignite.Abp.BlobManagement.EntityFrameworkCore;

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
    // still open. ConfigureBlobStoring below is what actually points Site's file containers at it; this
    // DependsOn is what makes that provider resolvable at all.
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
    // authentication, multi-tenancy and unit-of-work middleware everything else here does. SiteMcpModule
    // only contributes the site_* tools; the server itself (transport, /mcp, filters) comes with it from
    // Dignite.Abp.AspNetCore.Mcp, and is configured for this deployment in ConfigureMcp.
    typeof(SiteMcpModule),

    // Blob Management, in this process: Site's files are kept in it. Its containers - images, attachments and
    // theme-assets, the ones Site's fields name - are declared, with their storage provider, in ConfigureBlobStoring.
    // BlobManagementHttpApiModule serves what a browser reaches without Site in between - the upload with a ticket
    // Site's admin UI posts to, and the public read at /blobs that the site's pages, og:image and the admin UI's
    // previews load files from - and the container and directory APIs Site's file picker calls with the user's token.
    // Site is a trusted, in-process caller of the file service, so no OAuth client is granted anything. It has no MCP
    // tools: Site's own file tools call the same service.
    typeof(BlobManagementApplicationModule),
    typeof(BlobManagementHttpApiModule),
    typeof(BlobManagementEntityFrameworkCoreModule)
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

        // An MCP client authenticates by discovering this host's RFC 9728 metadata and echoing the resource
        // identifier it found there back as the RFC 8707 `resource` parameter (see ConfigureMcpResourceMetadata).
        // OpenIddict's ValidateResources handler checks that parameter against OpenIddictServerOptions.Resources
        // - a server-level allow-list, unrelated to the scope's Resources or the client's permissions - and that
        // list is empty unless something registers it, so *every* resource indicator is rejected with
        // invalid_target/ID2190. Registering App:SelfUrl is what makes the MCP authorization flow reachable.
        //
        // The value is registered as a Uri because that is how OpenIddict compares it: ValidateResources matches
        // against Uri.AbsoluteUri, which always carries a path, so "https://host:port" is normalised to
        // "https://host:port/" - the same form a client that round-trips the discovered value through a URL
        // parser (MCP Inspector does) sends back.
        var mcpResource = configuration["App:SelfUrl"]?.TrimEnd('/');
        if (!mcpResource.IsNullOrWhiteSpace())
        {
            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.RegisterResources(new Uri(mcpResource!, UriKind.Absolute));
            });
        }

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
        ConfigureMcp();
    }

    /// <summary>
    /// This deployment's settings for the application's MCP server - the server is shared by every
    /// module that contributes tools, so its identity and reach are the host's to set, not any one module's.
    /// </summary>
    private void ConfigureMcp()
    {
        Configure<AbpMcpServerOptions>(options =>
        {
            options.ServerName = "Dignite.Site";
        });
    }

    /// <summary>
    /// The storage provider for each <see cref="SiteFileContainerNames"/> container (GitHub issue #41):
    /// filesystem for dev. Each container is a Blob Management container with its preset (size, types, image handling);
    /// Blob Management declares managed containers in code only. The base path is the one Site stored its files under
    /// before they moved to Blob Management, so the bytes stored then stay where it looks for them. Production provider (Azure/S3/other) is still an open decision
    /// (#41) - swapping it later only touches this method, since Blob Management addresses blobs through IBlobContainer
    /// by container name, never by provider.
    /// </summary>
    private void ConfigureBlobStoring(IHostEnvironment hostingEnvironment)
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            // One base path for all containers: the filesystem provider already nests each container
            // under its own name-derived subdirectory.
            var basePath = Path.Combine(hostingEnvironment.ContentRootPath, "App_Data", "files");

            foreach (var containerName in SiteFileContainerNames.All)
            {
                options.Containers.Configure(containerName, container =>
                {
                    container.UseFileSystem(fileSystem => fileSystem.BasePath = basePath);

                    switch (containerName)
                    {
                        case SiteFileContainerNames.Images:
                            container.UseBlobManagementContainer(managed => managed.AllowDirectories = true);
                            container.UseImagesPreset();
                            break;
                        case SiteFileContainerNames.Attachments:
                            container.UseBlobManagementContainer(managed => managed.AllowDirectories = true);
                            container.UseDocumentsPreset();
                            break;
                        case SiteFileContainerNames.ThemeAssets:
                            container.UseBlobManagementContainer();
                            container.UseBinaryAssetsPreset();
                            break;
                        default:
                            throw new InvalidOperationException($"No Blob Management preset for Site's container '{containerName}'.");
                    }

                    // The sizes Site asks Blob Management's public read for (og:image's box, and a template's
                    // file_url rounded up to one of them); any other is answered 400.
                    container.UseBlobManagementPublicSizes(SiteImageSizes.All.ToArray());
                });
            }
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

        // Fails at startup rather than degrading: an MCP client that cannot discover where to authenticate
        // cannot connect at all, and a misconfigured deployment should hear about it here rather than from
        // that client. (AddAbpMcpAuthenticationDiscovery would refuse empty values too; this message names
        // the configuration key to fix.)
        if (selfUrl.IsNullOrWhiteSpace() || authority.IsNullOrWhiteSpace())
        {
            throw new AbpException(
                "App:SelfUrl must be configured. The MCP endpoint publishes RFC 9728 protected-resource "
                + "metadata describing its own origin, and cannot derive one. Set App:SelfUrl (and "
                + "optionally AuthServer:Authority, which defaults to it).");
        }

        // Publishes /.well-known/oauth-protected-resource and makes an unauthenticated /mcp request answer
        // 401 + WWW-Authenticate: Bearer resource_metadata="..." instead of this MVC host's default 302 to
        // /Account/Login. Only the challenge is taken over: authentication stays on the default policy -
        // the Identity cookie, which ForwardIdentityAuthenticationForBearer hands to OpenIddict for a bearer
        // token - so the principal that UseDynamicClaims refreshed is the one MCP tools are authorized
        // against. (Pinning the endpoint to the MCP scheme, as this host once did, re-authenticated through
        // it and replaced that principal with the raw token's.) It also hides the MCP scheme from the
        // Account module's external-login buttons (GitHub issue #47).
        context.Services.AddAbpMcpAuthenticationDiscovery(metadata =>
        {
            metadata.Resource = selfUrl!;
            metadata.AuthorizationServers.Add(authority!);
            metadata.ScopesSupported.Add(SiteHostConsts.ApiScopeName);
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
        // Blob Management's anonymous addresses - the public read and its resizing, the upload with a ticket - name
        // ASP.NET Core rate limiting policies, which only act where the limiter runs after routing.
        app.UseRateLimiter();
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
