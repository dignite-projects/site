using System.Threading.Tasks;
using Dignite.Site.Public.Templating;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>file_url url [width] [height]</c>: a site image's address at the size the template lays it out
/// (<see cref="SiteFileUrl.Sized"/>) - the public read endpoint resizes on request. Both sizes are optional and
/// <c>null</c> counts as not given (<c>file_url url null 200</c> is a height alone); with neither, the address
/// comes back as it is, which the endpoint serves at the size stored. Null in, null out.
/// </summary>
public class FileUrlFunction : SiteScriptFunction
{
    public const string Name = "file_url";

    public FileUrlFunction()
        : base(Required<string>("url"), Optional<object>("width"), Optional<object>("height"))
    {
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var url = arguments[0] as string;
        int? width = arguments[1] == null ? null : context.ToInt(span, arguments[1]);
        int? height = arguments[2] == null ? null : context.ToInt(span, arguments[2]);

        return new ValueTask<object?>(SiteFileUrl.Sized(url, width, height));
    }
}
