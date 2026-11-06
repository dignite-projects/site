using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
using Dignite.Site.Fields;
using Dignite.Site.Pages;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Routing;
using global::Scriban.Runtime;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// Turns a <see cref="SiteRenderViewModel"/> into the data a Scriban template sees - pure
/// <see cref="ScriptObject"/>/<see cref="ScriptArray"/>/primitive/<see cref="DateTime"/> values, never a CLR
/// object (see <see cref="ScribanSandbox"/>). DTOs go through <c>System.Text.Json</c> with snake_case member
/// names, Scriban's own convention (<c>page.display_name</c>), enums as snake_case strings.
/// <para>
/// The template vocabulary - the globals every template has:
/// <list type="table">
/// <item><term><c>page</c></term><description>The routed page: <c>id</c>, <c>name</c>, <c>display_name</c>,
/// <c>route</c>, <c>template</c>, <c>is_home_page</c>, <c>parent_id</c>, <c>is_active</c> and the audit members
/// (<c>creation_time</c>, ...). Never its content types.</description></item>
/// <item><term><c>content</c></term><description>Null on a page (list/index) match. Otherwise the content:
/// <c>id</c>, <c>page_id</c>, <c>content_type_id</c>, <c>culture_name</c>, <c>slug</c>, <c>publish_time</c>,
/// <c>status</c> (<c>"published"</c>), <c>url</c>, the audit members, <c>fields</c> - its field values keyed by
/// field name exactly as the site named them (<c>content.fields.title</c>), each value as JSON would carry it
/// (an object's members camelCase, as over HTTP) - and <c>field_list</c>, see below.</description></item>
/// <item><term><c>content_type</c></term><description>Null on a page match. Otherwise the content's type:
/// <c>id</c>, <c>name</c>, <c>display_name</c>, <c>description</c>, <c>fields</c> (each <c>field_id</c>,
/// <c>required</c>, <c>searchable</c>, <c>show_in_list</c>, <c>display_name</c>, <c>order</c>).</description></item>
/// <item><term><c>field_list</c></term><description>The content's fields in content-type order, SEO field
/// excluded, each <c>{ name, display_name, field_type_name, required, value }</c>; empty on a page
/// match.</description></item>
/// <item><term><c>culture</c>, <c>default_culture</c></term><description>The page's language and the site's
/// default one, e.g. <c>"ja"</c> and <c>"en"</c>.</description></item>
/// <item><term><c>languages</c></term><description>One per enabled language, in configured order:
/// <c>{ culture_name, native_name, url, is_current, is_translation }</c> - <c>native_name</c> is the language's
/// name in itself ("日本語"), <c>url</c> is this page's translation when it has one, that language's home page
/// otherwise.</description></item>
/// <item><term><c>filter_values</c></term><description>Every route placeholder value, keyed as the route names
/// it (<c>filter_values["publishTime:yyyy"]</c>).</description></item>
/// <item><term><c>field_filters</c></term><description>The same minus <c>publishTime</c> - what
/// <c>contents</c> filters the current page's list by.</description></item>
/// <item><term><c>published_after</c>, <c>published_before</c></term><description>The publish-time window a
/// <c>publishTime</c> placeholder denotes (an archive month), or null.</description></item>
/// <item><term><c>page_no</c></term><description>The page of a paged list the request asks for
/// (<see cref="SiteRenderViewModel.CurrentPage"/>): 1 unless the request's page query parameter says
/// otherwise. What a template passes <c>contents</c> as <c>page_no</c>; <c>site_head</c> and <c>page_url</c>
/// read it themselves.</description></item>
/// <item><term><c>query</c></term><description>The request's query string, each parameter's first value by its name in
/// lower case (<see cref="SiteRenderViewModel.Query"/>): <c>query.service</c> on <c>/start?service=design</c>, null for a
/// parameter the request does not have. A visitor's text: written HTML-encoded like every value.</description></item>
/// <item><term><c>head</c></term><description>The SEO head metadata, or null: <c>meta_title</c>,
/// <c>base_meta_title</c>, <c>meta_description</c>, <c>canonical_url</c>, <c>robots_content</c>,
/// <c>og_image_url</c>, <c>og_type</c>, <c>twitter_card_type</c>, <c>hreflang_alternates</c> (each
/// <c>culture_name</c>, <c>url</c>, <c>path</c>), <c>x_default_url</c>.</description></item>
/// </list>
/// Every time - <c>publish_time</c>, <c>creation_time</c>, <c>last_modification_time</c>,
/// <c>deletion_time</c>, <c>published_after</c>, <c>published_before</c> - is a real date, for <c>date.*</c>
/// and members such as <c>.year</c>.
/// </para>
/// </summary>
public class ScribanRenderModelBuilder : ITransientDependency
{
    /// <summary>How a DTO becomes template data: snake_case members, dictionary keys untouched, enums as snake_case strings.</summary>
    public static readonly JsonSerializerOptions DtoJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { ExcludeReshapedMembers } }
    };

    /// <summary>
    /// How a field value that is still a CLR object (in a single-process host) becomes JSON: the web defaults,
    /// so it has the same shape a remote host receives it in over HTTP.
    /// </summary>
    public static readonly JsonSerializerOptions FieldValueJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// How a field configuration that is still a CLR object becomes JSON: as the field type stores it, with no
    /// naming policy, so it has the member names a configuration read back from storage has.
    /// </summary>
    public static readonly JsonSerializerOptions ConfigurationJsonOptions = new();

    protected SiteLanguageContext LanguageContext { get; }
    protected SiteLanguageSwitcher LanguageSwitcher { get; }

    public ScribanRenderModelBuilder(SiteLanguageContext languageContext, SiteLanguageSwitcher languageSwitcher)
    {
        LanguageContext = languageContext;
        LanguageSwitcher = languageSwitcher;
    }

    /// <summary>The globals of one page render - see the class remarks for their shape.</summary>
    public virtual async Task<ScriptObject> BuildGlobalsAsync(SiteRenderViewModel viewModel)
    {
        var globals = new ScriptObject();

        globals.SetValue("page", BuildPage(viewModel.Page), false);

        var fieldList = viewModel.Content == null ? new ScriptArray() : BuildFieldList(viewModel.Content.Fields);
        globals.SetValue("content", viewModel.Content == null ? null : BuildContent(viewModel.Content, fieldList), false);
        globals.SetValue("content_type", viewModel.Content == null ? null : BuildContentType(viewModel.Content.ContentType), false);
        globals.SetValue("field_list", fieldList, false);

        var languages = await LanguageContext.GetLanguagesAsync();
        globals.SetValue("culture", viewModel.CultureName, false);
        globals.SetValue("default_culture", languages.DefaultCultureName, false);

        var languageItems = new ScriptArray();
        foreach (var item in await LanguageSwitcher.GetItemsAsync(viewModel.HeadMetadata))
        {
            languageItems.Add(FromDto(item));
        }
        globals.SetValue("languages", languageItems, false);

        globals.SetValue("filter_values", BuildStrings(viewModel.FilterValues), false);
        globals.SetValue("field_filters", BuildStrings(viewModel.FieldFilters), false);
        globals.SetValue("published_after", viewModel.PublishedAfter, false);
        globals.SetValue("published_before", viewModel.PublishedBefore, false);
        globals.SetValue("page_no", viewModel.CurrentPage, false);
        globals.SetValue("query", BuildQuery(viewModel.Query), false);
        globals.SetValue("head", viewModel.HeadMetadata == null ? null : FromDto(viewModel.HeadMetadata), false);

        var breadcrumb = new ScriptArray();
        foreach (var item in viewModel.Breadcrumb)
        {
            breadcrumb.Add(FromDto(item));
        }
        globals.SetValue("breadcrumb", breadcrumb, false);

        return globals;
    }

    /// <summary>
    /// One content as a template sees it: the content's own members, <c>fields</c> and <c>field_list</c> - the
    /// shape of <c>content</c>, and of each item <c>contents</c> returns.
    /// </summary>
    public virtual ScriptObject BuildContent(ContentRenderViewModel content)
    {
        return BuildContent(content, BuildFieldList(content.Fields));
    }

    protected virtual ScriptObject BuildContent(ContentRenderViewModel content, ScriptArray fieldList)
    {
        var dto = content.Content;
        var result = FromDto(dto);

        result.SetValue("publish_time", dto.PublishTime, false);
        SetAuditTimes(result, dto);

        var fields = new ScriptObject();
        // Null-safe throughout: a DTO that came over HTTP can carry a null where the C# initializer would not.
        foreach (var (name, value) in dto.FieldValues ?? new Dictionary<string, object?>())
        {
            fields.SetValue(name, FromFieldValue(value), false);
        }

        result.SetValue("fields", fields, false);
        result.SetValue("field_list", fieldList, false);

        return result;
    }

    protected virtual ScriptObject BuildPage(PageDto page)
    {
        var result = FromDto(page);
        result.SetValue("is_home_page", !string.IsNullOrEmpty(page.Route) && PageRoute.IsHomeRoute(page.Route), false);
        SetAuditTimes(result, page);
        return result;
    }

    protected virtual ScriptObject BuildContentType(ContentTypeDto contentType)
    {
        var result = FromDto(contentType);
        SetAuditTimes(result, contentType);
        return result;
    }

    /// <summary>
    /// A page <c>find_page</c> found, as a template sees it: the members of <c>page</c> and <c>content_types</c>,
    /// each shaped like <c>content_type</c> - its members and <c>fields</c>, the content type's use of each field
    /// (<c>field_id</c>, <c>required</c>, ...), whose definitions <c>field_definitions</c> resolves.
    /// </summary>
    public virtual ScriptObject BuildPageWithContentTypes(PageDto page)
    {
        var result = BuildPage(page);

        var contentTypes = new ScriptArray();
        foreach (var contentType in page.ContentTypes ?? new List<ContentTypeDto>())
        {
            contentTypes.Add(BuildContentType(contentType));
        }

        result.SetValue("content_types", contentTypes, false);
        return result;
    }

    /// <summary>
    /// Field definitions as <c>field_definitions</c> returns them: an object keyed by field name, each
    /// <c>{ id, name, display_name, description, field_type_name, configuration }</c>. <c>configuration</c> is
    /// the field type's own, JSON-shaped and named as the field type stored it - see <see cref="FromConfiguration"/>.
    /// </summary>
    public virtual ScriptObject BuildFieldDefinitions(IEnumerable<FieldDto> fields)
    {
        var result = new ScriptObject();
        foreach (var field in fields)
        {
            result.SetValue(field.Name, BuildFieldDefinition(field), false);
        }

        return result;
    }

    protected virtual ScriptObject BuildFieldDefinition(FieldDto field)
    {
        var result = new ScriptObject();
        result.SetValue("id", field.Id.ToString(), false);
        result.SetValue("name", field.Name, false);
        result.SetValue("display_name", field.DisplayName, false);
        result.SetValue("description", field.Description, false);
        result.SetValue("field_type_name", field.FieldTypeName, false);
        result.SetValue("configuration", FromConfiguration(field.Configuration), false);
        return result;
    }

    /// <summary>
    /// A field's configuration as template data, named as the field type stored it: its keys carry the field
    /// type's name (<c>Select.Options</c>), the members of an object inside are as stored too (<c>Text</c>,
    /// <c>Value</c>). It arrives from storage as <see cref="JsonElement"/>s, which keep their names; a value that is
    /// still a CLR object is serialized without a naming policy (<see cref="ConfigurationJsonOptions"/>), so it has
    /// the same names. Unlike a field value, it is not renamed to camelCase.
    /// </summary>
    public static ScriptObject FromConfiguration(IDictionary<string, object?>? configuration)
    {
        return configuration == null
            ? new ScriptObject()
            : ScribanValues.ObjectFromJson(
                JsonSerializer.SerializeToElement(configuration, configuration.GetType(), ConfigurationJsonOptions));
    }

    protected virtual ScriptArray BuildFieldList(IReadOnlyList<FlexFieldValue> fields)
    {
        var result = new ScriptArray(fields.Count);
        foreach (var field in fields)
        {
            var item = new ScriptObject();
            item.SetValue("name", field.Name, false);
            item.SetValue("display_name", field.DisplayName, false);
            item.SetValue("field_type_name", field.FieldTypeName, false);
            item.SetValue("required", field.Required, false);
            item.SetValue("value", FromFieldValue(field.Value), false);
            result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// A field value as template data. Over HTTP it arrives as a <see cref="JsonElement"/>; in a single-process
    /// host it may still be a CLR value, which is serialized first, so both hosts hand a template the same
    /// shape.
    /// </summary>
    public static object? FromFieldValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement element => ScribanValues.FromJson(element),
            _ => ScribanValues.FromJson(JsonSerializer.SerializeToElement(value, value.GetType(), FieldValueJsonOptions))
        };
    }

    protected static ScriptObject FromDto(object dto)
    {
        return ScribanValues.ObjectFromJson(JsonSerializer.SerializeToElement(dto, dto.GetType(), DtoJsonOptions));
    }

    /// <summary>
    /// The request's query parameters by name in lower case: the request's names are read without regard to case, as
    /// ASP.NET Core reads them, so <c>query.service</c> finds <c>?Service=design</c> too.
    /// </summary>
    protected static ScriptObject BuildQuery(IReadOnlyDictionary<string, string> query)
    {
        var result = new ScriptObject();
        foreach (var (name, value) in query)
        {
            result.SetValue(name.ToLowerInvariant(), value, false);
        }

        return result;
    }

    protected static ScriptObject BuildStrings(IReadOnlyDictionary<string, string>? values)
    {
        var result = new ScriptObject();
        if (values == null)
        {
            return result;
        }

        foreach (var (key, value) in values)
        {
            result.SetValue(key, value, false);
        }

        return result;
    }

    /// <summary>JSON carries the audit times as text; a template wants dates.</summary>
    protected static void SetAuditTimes(ScriptObject target, FullAuditedEntityDto<Guid> dto)
    {
        target.SetValue("creation_time", dto.CreationTime, false);
        target.SetValue("last_modification_time", dto.LastModificationTime, false);
        target.SetValue("deletion_time", dto.DeletionTime, false);
    }

    /// <summary>
    /// Members a template gets in another shape, or not at all: a content's <c>FieldValues</c> (re-added as
    /// <c>fields</c>, serialized with <see cref="FieldValueJsonOptions"/>), a page's <c>ContentTypes</c>, which
    /// route resolution leaves unloaded anyway, and a page's computed <c>IsHomePage</c>, whose getter throws on a
    /// page without a route (re-added null-safely by <see cref="BuildPage"/>).
    /// </summary>
    private static void ExcludeReshapedMembers(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        string[]? excluded = null;
        if (typeof(ContentDto).IsAssignableFrom(typeInfo.Type))
        {
            excluded = new[] { nameof(ContentDto.FieldValues) };
        }
        else if (typeof(PageDto).IsAssignableFrom(typeInfo.Type))
        {
            excluded = new[] { nameof(PageDto.ContentTypes), nameof(PageDto.IsHomePage) };
        }

        if (excluded == null)
        {
            return;
        }

        foreach (var property in typeInfo.Properties
                     .Where(p => p.AttributeProvider is MemberInfo member && excluded.Contains(member.Name))
                     .ToList())
        {
            typeInfo.Properties.Remove(property);
        }
    }
}
