using System.Threading.Tasks;
using Dignite.FlexFields.Site.Seo;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Fields;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace Dignite.Site.Seo;

/// <summary>
/// The SEO field preset seed, run a second time to prove the idempotency the shared seeding mechanism
/// depends on: a db-migration service calls every <c>IDataSeedContributor</c> again on every run, for
/// every existing tenant plus host, not just once ever (总体设计 §5.3, GitHub issue #14).
/// </summary>
public class SeoFieldPresetSeedContributor_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SeoFieldPresetSeedContributor _seedContributor;
    private readonly IFieldRepository _fieldRepository;
    private readonly FieldManager _fieldManager;

    public SeoFieldPresetSeedContributor_Tests()
    {
        _seedContributor = GetRequiredService<SeoFieldPresetSeedContributor>();
        _fieldRepository = GetRequiredService<IFieldRepository>();
        _fieldManager = GetRequiredService<FieldManager>();
    }

    /// <summary>Sanity check on the module's own bootstrap seeding, which every other test here builds on.</summary>
    [Fact]
    public async Task Should_Already_Be_Seeded_By_Normal_Test_Bootstrap()
    {
        var field = await WithUnitOfWorkAsync(() => _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName));

        field.ShouldNotBeNull();
        field!.FieldTypeName.ShouldBe(SeoFieldNames.ControlName);
        field.DisplayName.ShouldBe("SEO");
    }

    [Fact]
    public async Task Running_Again_Should_Not_Duplicate_Or_Throw()
    {
        var before = await WithUnitOfWorkAsync(() => _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName));

        await WithUnitOfWorkAsync(() => _seedContributor.SeedAsync(new DataSeedContext(null)));

        var after = await WithUnitOfWorkAsync(() => _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName));

        after.ShouldNotBeNull();
        after!.Id.ShouldBe(before!.Id);
    }

    /// <summary>
    /// A reseed must never win an edit war against a tenant's own customization - see
    /// <see cref="SeoFieldPresetSeedContributor"/>'s remarks.
    /// </summary>
    [Fact]
    public async Task Running_Again_Should_Not_Overwrite_A_Tenant_Customized_Field()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var field = (await _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName))!;
            await _fieldManager.UpdateAsync(field, "Our SEO", field.FieldTypeName, "Customized by the tenant");
        });

        await WithUnitOfWorkAsync(() => _seedContributor.SeedAsync(new DataSeedContext(null)));

        var reseeded = await WithUnitOfWorkAsync(() => _fieldRepository.FindByNameAsync(SeoFieldNames.FieldName));

        reseeded!.DisplayName.ShouldBe("Our SEO");
        reseeded.Description.ShouldBe("Customized by the tenant");
    }
}
