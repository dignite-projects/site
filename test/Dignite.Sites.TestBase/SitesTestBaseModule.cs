using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;

namespace Dignite.Sites;

/// <summary>
/// Shared test infrastructure - Autofac, ABP's test base, always-allow authorization, guid generation.
/// <para>
/// Deliberately does <b>not</b> seed data. <c>Dignite.Sites.Domain.Tests</c> and
/// <c>Dignite.Sites.Application.Tests</c> both build on this module and have no database - any
/// <c>IDataSeedContributor</c> that touches a repository (the normal case for a production contributor,
/// e.g. <c>SeoFieldPresetSeedContributor</c>) cannot be constructed there, since a repository's only
/// concrete registration is provider-specific (EF Core, MongoDB, ...) and neither test layer loads one.
/// Seeding is instead triggered by <c>SitesEntityFrameworkCoreTestModule</c> (and would be by a MongoDB
/// equivalent, once that provider has real repositories to seed into) - the layers that actually have a
/// database to seed.
/// </para>
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpGuidsModule)
)]
public class SitesTestBaseModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysAllowAuthorization();
    }
}
