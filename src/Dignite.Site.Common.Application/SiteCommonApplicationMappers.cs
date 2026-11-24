using System.Collections.Generic;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Dignite.Site.Common;

/// <summary>
/// Flat scalar-property entity-to-DTO mappers, shared by Admin and Public rather than one copy per module.
/// <para>
/// They used to be duplicated in <c>Dignite.Site.Admin.Application</c> and <c>Dignite.Site.Public.Application</c>
/// on the theory that <c>AddMapperlyObjectMapper&lt;TModule&gt;()</c> only scans <c>TModule</c>'s own
/// assembly, not its project references - true, but beside the point: Mapperly's <c>IObjectMapper</c>
/// registration is global across the whole DI container regardless of which module's scan found a given
/// <c>[Mapper]</c> class, so two modules each declaring the identical mapper for the same
/// source/destination pair does not give either module "its own" copy - it gives the container two
/// competing registrations for the same capability, and whichever loads last silently wins for every
/// caller, not just that module's own. This module's own <c>ConfigureServices</c> already calls
/// <c>AddMapperlyObjectMapper&lt;SiteCommonApplicationModule&gt;()</c>, and both
/// <c>SiteAdminApplicationModule</c> and <c>SitePublicApplicationModule</c> already <c>DependsOn</c> it - so one
/// copy declared here reaches both without either needing its own.
/// </para>
/// <para>
/// Each mapper produces the whole DTO, the FlexFields dictionaries and a content type's fields included
/// (through <c>FlexFieldValueDictionaryExtensions</c> and <c>ContentTypeFieldDtoExtensions</c>), so no
/// service finishes one by hand. Left out are what the entity does not hold: <c>ContentDto.Url</c>,
/// <c>PageName</c> and <c>ContentTypeName</c>, and <c>PageDto.ContentTypes</c>, which only a page read with
/// its details has (<c>PageDtoExtensions.ToDtoWithContentTypes</c>).
/// </para>
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
    public override partial ContentTypeDto Map(ContentType source);

    public override partial void Map(ContentType source, ContentTypeDto destination);

    private static List<ContentTypeFieldDto> MapFields(IReadOnlyList<ContentTypeField> fields) => fields.ToDtoList();
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FieldToFieldDtoMapper : MapperBase<Field, FieldDto>
{
    public override partial FieldDto Map(Field source);

    public override partial void Map(Field source, FieldDto destination);

    private static IDictionary<string, object?> MapConfiguration(FieldConfigurationDictionary configuration) =>
        configuration.ToValueDictionary();
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ContentToContentDtoMapper : MapperBase<Content, ContentDto>
{
    // Url has no source on Content itself - whichever Public service resolved the content's owning page
    // alongside it sets Url by hand afterward; every other producer (Admin included) leaves it at
    // ContentDto.Url's own string.Empty default. PageName and ContentTypeName are the same: names live on the
    // page and the content type, and the Admin service that reads those sets them by hand.
    [MapProperty(nameof(Content.FlexFields), nameof(ContentDto.FieldValues))]
    [MapperIgnoreTarget(nameof(ContentDto.Url))]
    [MapperIgnoreTarget(nameof(ContentDto.PageName))]
    [MapperIgnoreTarget(nameof(ContentDto.ContentTypeName))]
    public override partial ContentDto Map(Content source);

    [MapProperty(nameof(Content.FlexFields), nameof(ContentDto.FieldValues))]
    [MapperIgnoreTarget(nameof(ContentDto.Url))]
    [MapperIgnoreTarget(nameof(ContentDto.PageName))]
    [MapperIgnoreTarget(nameof(ContentDto.ContentTypeName))]
    public override partial void Map(Content source, ContentDto destination);

    private static IDictionary<string, object?> MapFieldValues(FlexFieldDictionary values) => values.ToValueDictionary();
}
