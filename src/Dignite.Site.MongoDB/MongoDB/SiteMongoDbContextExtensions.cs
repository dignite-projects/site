using Dignite.Site.Directories;
using Dignite.Site.Files;
using MongoDB.Bson;
using MongoDB.Driver;
using Volo.Abp;
using Volo.Abp.MongoDB;

namespace Dignite.Site.MongoDB;

public static class SiteMongoDbContextExtensions
{
    public static void ConfigureSite(
        this IMongoModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<DirectoryDescriptor>(x =>
        {
            x.CollectionName = SiteDbProperties.DbTablePrefix + "DirectoryDescriptors";
        });

        builder.Entity<FileDescriptor>(x =>
        {
            x.CollectionName = SiteDbProperties.DbTablePrefix + "FileDescriptors";
            x.ConfigureIndexes(indexes =>
            {
                var keys = Builders<BsonDocument>.IndexKeys;

                // A blob name is a descriptor's address: unique per tenant and container, deleted rows
                // included, since a name is never reused.
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.BlobName)),
                    new CreateIndexOptions { Unique = true }));

                // Content dedup's arbiter: one live owner per hash (the same rule as the EF Core filtered
                // index). A deleted owner keeps its hash and must not block the same bytes coming back.
                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.Hash)),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Unique = true,
                        PartialFilterExpression = Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Gt(nameof(FileDescriptor.Hash), string.Empty),
                            Builders<BsonDocument>.Filter.Eq(nameof(FileDescriptor.IsDeleted), false))
                    }));

                indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                    keys.Ascending(nameof(FileDescriptor.TenantId))
                        .Ascending(nameof(FileDescriptor.ContainerName))
                        .Ascending(nameof(FileDescriptor.ReferBlobName))));
            });
        });
    }
}
