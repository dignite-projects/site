using System.Threading.Tasks;
using Dignite.FlexFields.Site.Seo;
using Dignite.Site.Fields;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Seo;

/// <summary>
/// Seeds the platform's SEO field into a tenant's field library (总体设计 §5.3, GitHub issue #14).
/// <para>
/// Idempotent by design, not by a one-time flag: the host's db-migration service calls
/// <c>IDataSeeder.SeedAsync(tenantId)</c> for every existing tenant (plus host) on every run, and
/// ABP's <c>TenantAppService.CreateAsync</c> calls the same for a freshly created tenant - so this one
/// idempotent contributor covers both "a new tenant" and "an existing tenant that predates this feature"
/// without any bespoke provisioning path. Existence is checked by <see cref="SeoFieldNames.FieldName"/>
/// rather than an id, for the same reason <c>FieldManager</c>'s deletion/rename guard does: there is no
/// literal shared <c>Guid</c> to check against (see that guard's remarks).
/// </para>
/// <para>
/// Does not touch an already-seeded field, even to refresh its <c>DisplayName</c>/<c>Configuration</c> to
/// whatever this class currently sets by default - a tenant's own customization of those always wins.
/// </para>
/// <para>
/// No current-tenant switching here - <c>IDataSeeder.SeedAsync(tenantId)</c> already sets the ambient
/// tenant before invoking every contributor (see how <see cref="FieldManager.CreateAsync"/> is written to
/// rely on <c>CurrentTenant.Id</c> without being told it explicitly).
/// </para>
/// <para>
/// <b>Lives in Domain, matching ABP's own convention</b> - e.g. <c>Volo.Abp.Identity</c>'s
/// <c>IdentityDataSeedContributor</c> lives in <c>Volo.Abp.Identity.Domain</c>, not in
/// <c>.EntityFrameworkCore</c> or <c>.MongoDB</c>, even though it also needs repository access
/// (<c>IIdentityUserRepository</c> et al.) - a seed contributor only needs repository *interfaces*, and
/// does not care which provider's concrete registration ends up satisfying them. This class is the same
/// shape: it only depends on <see cref="FieldManager"/>/<see cref="IFieldRepository"/>, both defined here
/// in Domain. <c>Dignite.Site.Domain.Tests</c> has no database and would fail to construct this (ABP
/// discovers every <c>IDataSeedContributor</c> by scanning loaded assemblies for the type itself,
/// independent of <c>ITransientDependency</c>, and resolves each one by its concrete type) if anything
/// there triggered seeding - which is why seeding is triggered only from
/// <c>SiteEntityFrameworkCoreTestModule</c>, not from the shared <c>SiteTestBaseModule</c> every test
/// layer builds on. See that module's remarks for the full reasoning.
/// </para>
/// </summary>
public class SeoFieldPresetSeedContributor : IDataSeedContributor, ITransientDependency
{
    protected FieldManager FieldManager { get; }

    protected IFieldRepository FieldRepository { get; }

    public SeoFieldPresetSeedContributor(FieldManager fieldManager, IFieldRepository fieldRepository)
    {
        FieldManager = fieldManager;
        FieldRepository = fieldRepository;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (await FieldRepository.FindByNameAsync(SeoFieldNames.FieldName) != null)
        {
            return;
        }

        await FieldManager.CreateAsync(
            SeoFieldNames.FieldName,
            "SEO",
            SeoFieldNames.ControlName,
            description:
            "Platform-recognized SEO metadata: meta title/description (aim for this field's char " +
            "limits - engines truncate, don't reject), a social share image, and noindex (excludes " +
            "this content from the sitemap - use for duplicates or thank-you pages).",
            configuration: new SeoConfiguration().ConfigurationDictionary);
    }
}
