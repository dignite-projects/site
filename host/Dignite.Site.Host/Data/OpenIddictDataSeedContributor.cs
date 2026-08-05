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
        }
    }

    private async Task CreateApiScopeAsync()
    {
        if (await _scopeManager.FindByNameAsync(HostConsts.ApiScopeName) == null)
        {
            await _scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = HostConsts.ApiScopeName,
                DisplayName = "Host API",
                Resources = { HostConsts.ApiScopeName }
            });
        }
    }

    private async Task CreateAngularApplicationAsync()
    {
        var angularRootUrl =
            _configuration["OpenIddict:Applications:Host_App:RootUrl"]?.TrimEnd('/')
            ?? "http://localhost:4200";

        await CreateApplicationAsync(
            name: HostConsts.AngularClientId,
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
                HostConsts.ApiScopeName
            },
            clientUri: angularRootUrl,
            redirectUri: angularRootUrl,
            postLogoutRedirectUri: angularRootUrl
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
        string? postLogoutRedirectUri = null)
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

        if (await _applicationManager.FindByClientIdAsync(name) != null)
        {
            return;
        }

        var application = new AbpApplicationDescriptor
        {
            ClientId = name,
            ClientType = type,
            ClientSecret = secret,
            ConsentType = consentType,
            DisplayName = displayName,
            ClientUri = clientUri,
        };

        Check.NotNullOrEmpty(grantTypes, nameof(grantTypes));
        Check.NotNullOrEmpty(scopes, nameof(scopes));

        if (!redirectUri.IsNullOrWhiteSpace() || !postLogoutRedirectUri.IsNullOrWhiteSpace())
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

        if (!redirectUri.IsNullOrWhiteSpace())
        {
            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) || !uri.IsWellFormedOriginalString())
            {
                throw new BusinessException(L["InvalidRedirectUri", redirectUri]);
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

        await _applicationManager.CreateAsync(application);
    }
}
