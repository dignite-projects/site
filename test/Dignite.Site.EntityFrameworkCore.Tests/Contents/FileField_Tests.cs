using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.FlexFields.Site.Files;
using Dignite.Site.Admin.Schema;
using Dignite.Site.ContentTypes;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Files;
using Dignite.Site.Fields;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dignite.Site.Contents;

/// <summary>
/// GitHub issue #42: the file field type (registration key "FileExplorer"), pointed at one of Site's file
/// containers. Unlike CKEditor's plain-string value, a meaningful round trip here has to reference a file
/// that actually exists - so this creates one for real through <see cref="FileDescriptorManager"/> first,
/// the same backend <c>FileDescriptorManager_Tests</c> proves, rather than fabricating a descriptor-shaped
/// value nothing backs.
/// </summary>
public class FileField_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IRepository<Field, Guid> _fieldRepository;
    private readonly IRepository<ContentType, Guid> _contentTypeRepository;
    private readonly IContentRepository _contentRepository;
    private readonly ContentManager _contentManager;
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly ISiteSchemaAdminAppService _schemaAppService;

    public FileField_Tests()
    {
        _fieldRepository = GetRequiredService<IRepository<Field, Guid>>();
        _contentTypeRepository = GetRequiredService<IRepository<ContentType, Guid>>();
        _contentRepository = GetRequiredService<IContentRepository>();
        _contentManager = GetRequiredService<ContentManager>();
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _schemaAppService = GetRequiredService<ISiteSchemaAdminAppService>();
    }

    [Fact]
    public async Task Should_Round_Trip_A_Reference_To_A_Real_File_Unchanged()
    {
        var fieldName = await WithUnitOfWorkAsync(async () =>
        {
            var (name, contentTypeId) = await CreateFileFieldAndContentTypeAsync();

            using var stream = new MemoryStream(TestFiles.Pdf("issue #42 attachment"));
            var file = await _fileDescriptorManager.CreateAsync(SiteFileContainerNames.Default, "notes.pdf", stream);

            var value = new List<FileReference> { FileReference.From(file) };

            var content = await _contentManager.CreateAsync(
                contentTypeId, SiteTestData.EnglishCulture, "post-with-attachment",
                SiteTestData.PublishTime, ContentStatus.Published,
                new Dictionary<string, object?> { [name] = value });

            content.GetField<List<FileReference>>(name).Single().BlobName.ShouldBe(file.BlobName);
            return name;
        });

        var reread = await WithUnitOfWorkAsync(() => _contentRepository.FindBySlugAsync(
            SiteTestData.BlogPageId, SiteTestData.EnglishCulture, "post-with-attachment"));

        reread.ShouldNotBeNull();
        var readBack = reread!.GetField<List<FileReference>>(fieldName).Single();
        readBack.ContainerName.ShouldBe(SiteFileContainerNames.Default);
        readBack.Name.ShouldBe("notes.pdf");
    }

    [Fact]
    public async Task Should_Expose_The_Field_And_Its_Container_In_The_Site_Schema()
    {
        var (fieldName, contentTypeName) = await WithUnitOfWorkAsync(async () =>
        {
            var (name, contentTypeId) = await CreateFileFieldAndContentTypeAsync();
            var contentType = await _contentTypeRepository.GetAsync(contentTypeId);
            return (name, contentType.Name);
        });

        var schema = await _schemaAppService.GetAsync();

        var attachmentType = schema.Pages
            .Single(page => page.Name == "blog").ContentTypes
            .Single(ct => ct.Name == contentTypeName);

        var field = attachmentType.Fields.Single(f => f.Name == fieldName);
        // The registration and configuration keys are persisted data, unchanged from when the field type
        // lived in Dignite.Abp.FlexFields.
        field.FieldTypeName.ShouldBe(FileFieldType.ControlName);
        field.FieldTypeName.ShouldBe("FileExplorer");
        field.Configuration[FileFieldConfigurationNames.FileContainerName].ShouldBe(SiteFileContainerNames.Default);
        field.Configuration.ShouldContainKey("FileExplorer.FileContainerName");
    }

    private async Task<(string FieldName, Guid ContentTypeId)> CreateFileFieldAndContentTypeAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        var fieldName = $"attachment-{token}";

        var configuration = new FileFieldConfiguration { FileContainerName = SiteFileContainerNames.Default };

        var field = await _fieldRepository.InsertAsync(
            new Field(Guid.NewGuid(), fieldName, "Attachment", FileFieldType.ControlName, configuration: configuration.ConfigurationDictionary),
            autoSave: true);

        var contentType = await _contentTypeRepository.InsertAsync(
            new ContentType(
                Guid.NewGuid(), SiteTestData.BlogPageId, $"post-attachment-{token}", "Post with attachment",
                fields: new[]
                {
                    new ContentTypeField(
                        field.Id, required: false, searchable: false, showInList: true,
                        displayName: null, order: 0)
                }),
            autoSave: true);

        return (fieldName, contentType.Id);
    }

    /// <summary>
    /// A minimal stand-in for the shape the admin UI's file picker hands over - the field type itself only
    /// checks presence (see FileFieldType's doc comment), so this test defines its own shape.
    /// </summary>
    private class FileReference
    {
        public Guid Id { get; set; }
        public string ContainerName { get; set; } = string.Empty;
        public string BlobName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public static FileReference From(FileDescriptor file) => new()
        {
            Id = file.Id,
            ContainerName = file.ContainerName,
            BlobName = file.BlobName,
            Name = file.Name
        };
    }
}
