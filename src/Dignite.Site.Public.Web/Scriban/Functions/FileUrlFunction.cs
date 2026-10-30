using System.Threading.Tasks;
using Dignite.Site.Public.Templating;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>file_url url width [height]</c>: a site image's address at the size the template lays it out
/// (<see cref="SiteFileUrl.Sized"/>) - the public read endpoint resizes on request. Null in, null out.
/// </summary>
public class FileUrlFunction : SiteScriptFunction
{
    public const string Name = "file_url";

    public FileUrlFunction()
        : base(Required<string>("url"), Required<int>("width"), Optional<object>("height"))
    {
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var url = arguments[0] as string;
        var width = (int)arguments[1]!;
        int? height = arguments[2] == null ? null : context.ToInt(span, arguments[2]);

        return new ValueTask<object?>(SiteFileUrl.Sized(url, width, height));
    }
}
