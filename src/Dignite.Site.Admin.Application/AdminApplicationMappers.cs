using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Dignite.Site.Admin;

/// <summary>
/// Flat scalar-property entity-to-DTO mappers. Anything that needs a dictionary conversion
/// (<c>FieldDto.Configuration</c>, <c>ContentDto.FieldValues</c>) or a value-object list conversion
/// (<c>ContentTypeDto.Fields</c>) is ignored here and set by hand in the owning app service - see
/// <c>Dignite.Site.Common.FlexFieldValueDictionaryExtensions</c> and
/// <c>Dignite.Site.Common.ContentTypeFieldDtoExtensions</c>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class PageToPageDtoMapper : MapperBase<Page, PageDto>
{
    [MapperIgnoreTarget(nameof(PageDto.ContentTypes))]
    public override partial PageDto Map(Page source);

    [MapperIgnoreTarget(nameof(PageDto.ContentTypes))]
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
