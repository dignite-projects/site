using System;
using System.Threading.Tasks;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>app_file_url path</c>: the address of a file the application itself serves from its web root - not a file of the
/// theme, which <c>asset_url</c> addresses - with a version taken from the file's content, as a Razor view's
/// <c>asp-append-version</c> writes it (<see cref="IFileVersionProvider"/>): <c>/libs/abp/core/abp.js?v=...</c>, under the
/// application's path base. A new version of the file changes the address, so a long browser cache never serves the old
/// one. A file the web root does not have gets no version.
/// </summary>
public class AppFileUrlFunction : SiteScriptFunction
{
    public const string Name = "app_file_url";

    protected ScribanRenderScope Scope { get; }

    public AppFileUrlFunction(ScribanRenderScope scope)
        : base(Required<string>("path"))
    {
        Scope = scope;
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var path = (arguments[0] as string)?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            throw new ScriptRuntimeException(span, "app_file_url takes the path of a file of the application's web root, e.g. app_file_url \"/libs/abp/core/abp.js\".");
        }

        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            path = path[1..];
        }

        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal))
        {
            throw new ScriptRuntimeException(span, $"app_file_url takes a path of the application's own, starting with /, not '{path}'.");
        }

        var httpContext = Scope.ActionContext.HttpContext;
        var pathBase = httpContext.Request.PathBase;
        var resolved = pathBase.Add(new PathString(path.Split('?', 2)[0])).Value + (path.Contains('?') ? path[path.IndexOf('?')..] : string.Empty);

        var versions = httpContext.RequestServices?.GetService<IFileVersionProvider>();
        return new ValueTask<object?>(versions == null ? resolved : versions.AddFileVersionToPath(pathBase, resolved));
    }
}
