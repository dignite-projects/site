using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Dignite.Site.Public;

/// <summary>
/// Flat scalar-property entity-to-DTO mappers - the read-only half of what Admin's own copy declares (no
/// <c>FieldGroup</c>, since Public never exposes it). Duplicated here rather than shared from Common,
/// because Mapperly's ABP registration scans only the registering module's own assembly
/// (<c>AddMapperlyObjectMapper&lt;PublicApplicationModule&gt;()</c>), not its project references.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PageToPageDtoMapper : MapperBase<Page, PageDto>
{
    public override partial PageDto Map(Page source);

    public override partial void Map(Page source, PageDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ContentTypeToContentTypeDtoMapper : MapperBase<ContentType, ContentTypeDto>
{
    [MapperIgnoreTarget(nameof(ContentTypeDto.Fields))]
    public override partial ContentTypeDto Map(ContentType source);

    [MapperIgnoreTarget(nameof(ContentTypeDto.Fields))]
    public override partial void Map(ContentType source, ContentTypeDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FieldToFieldDtoMapper : MapperBase<Field, FieldDto>
{
    [MapperIgnoreTarget(nameof(FieldDto.Configuration))]
    public override partial FieldDto Map(Field source);

    [MapperIgnoreTarget(nameof(FieldDto.Configuration))]
    public override partial void Map(Field source, FieldDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ContentToContentDtoMapper : MapperBase<Content, ContentDto>
{
    [MapperIgnoreTarget(nameof(ContentDto.FieldValues))]
    public override partial ContentDto Map(Content source);

    [MapperIgnoreTarget(nameof(ContentDto.FieldValues))]
    public override partial void Map(Content source, ContentDto destination);
}
