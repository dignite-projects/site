using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using OpenIddict.Abstractions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.OpenIddict.Localization;
using Volo.Abp.Uow;

namespace Dignite.Site.Host.Data;

public class OpenIddictDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IConfiguration _configuration;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAbpApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly IStringLocalizer<OpenIddictResponse> L;

    public OpenIddictDataSeedContributor(
        IConfiguration configuration,
        ICurrentTenant currentTenant,
        IAbpApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        IStringLocalizer<OpenIddictResponse> l)
    {
        _configuration = configuration;
        _currentTenant = currentTenant;
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
        L = l;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        // OpenIddict applications/scopes are host-side only, regardless of which tenant triggered seeding.
        using (_currentTenant.Change(null))
        {
            await CreateApiScopeAsync();
            await CreateAngularApplicationAsync();
            await CreateMcpApplicationsAsync();
        }
    }

    private async Task CreateApiScopeAsync()
    {
        if (await _scopeManager.FindByNameAsync(SiteHostConsts.ApiScopeName) == null)
        {
            await _scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = SiteHostConsts.ApiScopeName,
                DisplayName = "Host API",
                Resources = { SiteHostConsts.ApiScopeName }
            });
        }
    }

    private async Task CreateAngularApplicationAsync()
    {
        var angularRootUrl =
            _configuration["OpenIddict:Applications:Host_App:RootUrl"]?.TrimEnd('/')
            ?? "http://localhost:4200";

        await CreateApplicationAsync(
            name: SiteHostConsts.AngularClientId,
            type: OpenIddictConstants.ClientTypes.Public,
            consentType: OpenIddictConstants.ConsentTypes.Implicit,
            displayName: "Host Angular Application",
            secret: null,
            grantTypes: new List<string>
            {
                OpenIddictConstants.GrantTypes.AuthorizationCode,
                OpenIddictConstants.GrantTypes.RefreshToken
            },
            scopes: new List<string>
            {
                OpenIddictConstants.Permissions.Scopes.Address,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Phone,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Scopes.Roles,
                OpenIddictConstants.Scopes.OfflineAccess,
                SiteHostConsts.ApiScopeName
            },
            clientUri: angularRootUrl,
            redirectUri: angularRootUrl,
            postLogoutRedirectUri: angularRootUrl
        );
    }

    /// <summary>
    /// The two clients the MCP endpoint is reached through (总体设计 §6.2.5, GitHub issue #26).
    /// <para>
    /// Both live in the same OpenIddict authorization server as everything else and request the same
    /// <c>Host</c> scope, because "the same tokens as the HTTP API" is what makes ABP's permission checks
    /// and tenant resolution work over MCP with no new code: <c>ICurrentUser</c> and the token's
    /// <c>tenantid</c> claim are read exactly as they are for an HTTP request.
    /// </para>
    /// </summary>
    private async Task CreateMcpApplicationsAsync()
    {
        var scopes = new List<string>
        {
            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.Roles,
            OpenIddictConstants.Scopes.OfflineAccess,
            SiteHostConsts.ApiScopeName
        };

        // A public client: a desktop AI assistant cannot keep a secret, so it uses authorization_code
        // with PKCE and a loopback redirect.
        //
        // ApplicationType matters more than it looks. A desktop client binds an ephemeral loopback port
        // it cannot know in advance, so the registered URIs below carry no port. OpenIddict only ignores
        // the port when comparing loopback redirect URIs *if the application is native*
        // (OpenIddictApplicationManager.ValidateRedirectUriAsync gates that whole comparison on
        // HasApplicationTypeAsync(..., "native")); the default is "web", which falls back to exact
        // ordinal string equality and would reject every real callback.
        await CreateApplicationAsync(
            name: SiteHostConsts.McpClientId,
            type: OpenIddictConstants.ClientTypes.Public,
            applicationType: OpenIddictConstants.ApplicationTypes.Native,
            // Explicit, not Implicit: unlike the first-party Angular app, this token is handed to a
            // third-party AI client, and the user should see what it is being granted.
            consentType: OpenIddictConstants.ConsentTypes.Explicit,
            displayName: "Site MCP Client",
            secret: null,
            grantTypes: new List<string>
            {
                OpenIddictConstants.GrantTypes.AuthorizationCode,
                OpenIddictConstants.GrantTypes.RefreshToken
            },
            scopes: scopes,
            redirectUris: new List<string>
            {
                "http://localhost/callback",
                "http://127.0.0.1/callback",
                "http://[::1]/callback"
            },
            // PKCE is *required*, not merely supported. Without this the authorization server happily
            // issues and redeems a code for a secretless client on a loopback redirect with no
            // code_challenge at all - and a loopback redirect is precisely the case another local process
            // can race for the code (RFC 8252 §8.1). Declaring the client public and native describes the
            // threat; this is what actually defends against it.
            requirePkce: true
        );

        // A confidential client for unattended publishing. The secret is a development default and is
        // meant to be replaced per deployment, the same way the Angular client's URLs are.
        //
        // It is seeded with NO permissions on purpose, so out of the box every Admin tool refuses it and
        // its tools/list is reduced to the one tool that carries no permission of its own, resolve_path -
        // which reads published content only, i.e. what any visitor can already see. A client-credentials
        // token carries no user, so ABP resolves its permissions from the client grant instead - an
        // operator grants exactly what a given deployment's automation needs (provider name "C", provider
        // key = this client id). Seeding Admin.* here would hand full authoring rights to a client whose
        // secret is a published default.
        await CreateApplicationAsync(
            name: SiteHostConsts.McpServiceClientId,
            type: OpenIddictConstants.ClientTypes.Confidential,
            consentType: OpenIddictConstants.ConsentTypes.Implicit,
            displayName: "Site MCP Service Client",
            secret: _configuration["OpenIddict:Applications:Site_Mcp_Service:Secret"] ?? "1q2w3e*",
            grantTypes: new List<string> { OpenIddictConstants.GrantTypes.ClientCredentials },
            scopes: new List<string> { SiteHostConsts.ApiScopeName }
        );
    }

    private async Task CreateApplicationAsync(
        string name,
        string type,
        string consentType,
        string displayName,
        string? secret,
        List<string> grantTypes,
        List<string> scopes,
        string? clientUri = null,
        string? redirectUri = null,
        string? postLogoutRedirectUri = null,
        List<string>? redirectUris = null,
        string? applicationType = null,
        bool requirePkce = false)
    {
        if (!string.IsNullOrEmpty(secret) &&
            string.Equals(type, OpenIddictConstants.ClientTypes.Public, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(L["NoClientSecretCanBeSetForPublicApplications"]);
        }

        if (string.IsNullOrEmpty(secret) &&
            string.Equals(type, OpenIddictConstants.ClientTypes.Confidential, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(L["TheClientSecretIsRequiredForConfidentialApplications"]);
        }

        // Looked up here, but the create/update decision is made at the very end, after `application`
        // below is fully populated - so both paths apply from exactly the same descriptor and can never
        // drift apart from each other.
        var existingApplication = await _applicationManager.FindByClientIdAsync(name);

        var application = new AbpApplicationDescriptor
        {
            ClientId = name,
            ClientType = type,
            ClientSecret = secret,
            ConsentType = consentType,
            DisplayName = displayName,
            ClientUri = clientUri,
            ApplicationType = applicationType,
        };

        if (requirePkce)
        {
            application.Requirements.Add(
                OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        }

        Check.NotNullOrEmpty(grantTypes, nameof(grantTypes));
        Check.NotNullOrEmpty(scopes, nameof(scopes));

        var allRedirectUris = new List<string>();
        if (!redirectUri.IsNullOrWhiteSpace())
        {
            allRedirectUris.Add(redirectUri!);
        }

        if (redirectUris != null)
        {
            allRedirectUris.AddRange(redirectUris);
        }

        // Assembled first, then used as the gate. Reading only the scalar `redirectUri` here would deny
        // EndSession to any client that supplies its callbacks through the list instead.
        if (allRedirectUris.Count > 0 || !postLogoutRedirectUri.IsNullOrWhiteSpace())
        {
            application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        }

        foreach (var grantType in grantTypes)
        {
            if (grantType == OpenIddictConstants.GrantTypes.AuthorizationCode)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                application.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            }

            if (grantType == OpenIddictConstants.GrantTypes.ClientCredentials)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            }

            if (grantType == OpenIddictConstants.GrantTypes.AuthorizationCode ||
                grantType == OpenIddictConstants.GrantTypes.RefreshToken)
            {
                application.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
                application.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
            }
        }

        var builtInScopes = new[]
        {
            OpenIddictConstants.Permissions.Scopes.Address,
            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Phone,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.Roles
        };

        foreach (var scope in scopes)
        {
            application.Permissions.Add(
                builtInScopes.Contains(scope)
                    ? scope
                    : OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        foreach (var candidate in allRedirectUris)
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || !uri.IsWellFormedOriginalString())
            {
                throw new BusinessException(L["InvalidRedirectUri", candidate]);
            }

            application.RedirectUris.Add(uri);
        }

        if (!postLogoutRedirectUri.IsNullOrWhiteSpace())
        {
            if (!Uri.TryCreate(postLogoutRedirectUri, UriKind.Absolute, out var uri) || !uri.IsWellFormedOriginalString())
            {
                throw new BusinessException(L["InvalidPostLogoutRedirectUri", postLogoutRedirectUri]);
            }

            application.PostLogoutRedirectUris.Add(uri);
        }

        // Update, not skip, when the client already exists. `FindByClientIdAsync` succeeding was being
        // read as "already correct" rather than merely "already present" - a client seeded before a fix
        // such as ApplicationType=Native or requirePkce (both landed in earlier rounds of 总体设计
        // §6.2.7's review) would otherwise keep whatever it was first created with forever, on every
        // subsequent deploy, with no error or log to say so. UpdateAsync re-applies every
        // descriptor-driven field - Permissions, RedirectUris, ApplicationType, Requirements included -
        // from the same descriptor CreateAsync would have used. This is the same pattern
        // Volo.Abp.OpenIddict.OpenIddictDataSeedContributorBase.CreateOrUpdateApplicationAsync ships with
        // (it is not subclassed here only because it has no parameter for Requirements/PKCE).
        if (existingApplication != null)
        {
            await _applicationManager.UpdateAsync(existingApplication, application);
        }
        else
        {
            await _applicationManager.CreateAsync(application);
        }
    }
}
