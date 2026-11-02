using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Public.Fields;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>field_definitions fields</c>: the definitions of fields, an object keyed by field name, each
/// <c>{ id, name, display_name, description, field_type_name, configuration }</c> (see
/// <see cref="ScribanRenderModelBuilder.BuildFieldDefinitions"/>). <c>fields</c> is a content type's list of
/// fields (<c>content_type.fields</c>, or one of <c>find_page</c>'s <c>content_types</c>), or a list of field ids;
/// a single field or id does as well. At most <see cref="MaxFields"/> fields at a time.
/// <para>
/// Only fields a content type of this render names are resolved - the current content's, and those of the pages
/// <c>find_page</c> returned - as the public field service is scoped to ids a content type already named, not to
/// the whole field library. Any other id, an id that is not an id and an id with no field are left out. A definition
/// is fetched once per render.
/// </para>
/// </summary>
public class FieldDefinitionsFunction : SiteScriptFunction
{
    public const string Name = "field_definitions";

    /// <summary>The most fields a template may ask for in one call.</summary>
    public const int MaxFields = 100;

    protected ScribanRenderScope Scope { get; }
    protected IFieldPublicAppService FieldAppService { get; }
    protected ScribanRenderModelBuilder ModelBuilder { get; }

    public FieldDefinitionsFunction(ScribanRenderScope scope, IFieldPublicAppService fieldAppService, ScribanRenderModelBuilder modelBuilder)
        : base(Required<object>("fields"))
    {
        Scope = scope;
        FieldAppService = fieldAppService;
        ModelBuilder = modelBuilder;
    }

    protected override async ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var requested = new List<Guid>();
        Collect(context, span, arguments[0], requested);
        requested = requested.Distinct().ToList();

        if (requested.Count > MaxFields)
        {
            throw new ScriptRuntimeException(span, $"field_definitions takes at most {MaxFields} fields, not {requested.Count}.");
        }

        var ids = requested.Where(Scope.NamedFieldIds.Contains).ToList();

        var missing = ids.Where(id => !Scope.FieldsById.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            foreach (var field in (await FieldAppService.GetListAsync(missing)).Items)
            {
                Scope.FieldsById[field.Id] = field;
            }

            foreach (var id in missing)
            {
                Scope.FieldsById.TryAdd(id, null);
            }
        }

        return ModelBuilder.BuildFieldDefinitions(
            ids.Select(id => Scope.FieldsById[id]).Where(field => field != null).Select(field => field!));
    }

    /// <summary>The ids in <paramref name="value"/>: a list, or one id or field; what is neither is skipped.</summary>
    private static void Collect(TemplateContext context, SourceSpan span, object? value, List<Guid> ids)
    {
        switch (value)
        {
            case Guid id:
                ids.Add(id);
                break;
            case string text:
                if (Guid.TryParse(text, out var parsed))
                {
                    ids.Add(parsed);
                }

                break;
            case ScriptObject field:
                // A content type's use of a field: { field_id, required, ... }.
                if (field.TryGetValue(context, span, "field_id", out var fieldId))
                {
                    Collect(context, span, fieldId, ids);
                }

                break;
            case IEnumerable items:
                foreach (var item in items)
                {
                    Collect(context, span, item, ids);
                }

                break;
        }
    }
}
