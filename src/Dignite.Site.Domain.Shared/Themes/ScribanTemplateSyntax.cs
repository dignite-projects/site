using System;
using System.Collections.Generic;
using System.Linq;
using Scriban.Parsing;
using Scriban.Syntax;

namespace Dignite.Site.Themes;

/// <summary>
/// Parsing and static reading of tenant-authored Scriban templates. Lives in Domain.Shared, next to the package
/// reference, because the check before a publish and the renderer must share one parser: a template that passes the
/// check has to be a template that renders, and Public.Web may reference nothing but Domain.Shared.
/// <para>
/// <b>Front matter.</b> A template whose very first line is <c>+++</c> starts with a front matter block closed by
/// another <c>+++</c> line, parsed by Scriban's own front matter mode; anything else is parsed as an ordinary
/// template. The front matter is never executed - Scriban keeps it out of the rendered page - and is read here,
/// statically, for its one key, <c>layout</c> (<see cref="ReadFrontMatter"/>).
/// </para>
/// </summary>
public static class ScribanTemplateSyntax
{
    private static readonly LexerOptions FrontMatterLexerOptions = new()
    {
        Mode = ScriptMode.FrontMatterAndContent,
        FrontMatterMarker = SiteThemeConsts.FrontMatterMarker
    };

    /// <summary>
    /// Parses <paramref name="body"/> with Scriban's default (Scriban, not Liquid) options, in front matter mode when
    /// it starts with one (<see cref="HasFrontMatter"/>). The result may have errors - see <see cref="GetErrors"/> -
    /// in which case its syntax tree must not be relied on (it can even be missing).
    /// </summary>
    public static Scriban.Template Parse(string body, string? sourceFilePath = null)
    {
        body ??= string.Empty;

        return HasFrontMatter(body)
            ? Scriban.Template.Parse(body, sourceFilePath, parserOptions: null, lexerOptions: FrontMatterLexerOptions)
            : Scriban.Template.Parse(body, sourceFilePath);
    }

    /// <summary>Whether the first line of <paramref name="body"/> is exactly <c>+++</c>.</summary>
    public static bool HasFrontMatter(string body)
    {
        var marker = SiteThemeConsts.FrontMatterMarker;
        if (body == null || !body.StartsWith(marker, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = body.AsSpan(marker.Length);
        return rest.IsEmpty || rest[0] == '\n' || (rest.Length > 1 && rest[0] == '\r' && rest[1] == '\n');
    }

    /// <summary>The syntax errors of <paramref name="body"/>, or an empty list when it is valid.</summary>
    public static IReadOnlyList<ScribanTemplateError> GetErrors(string body)
    {
        return GetErrors(Parse(body));
    }

    /// <summary>The syntax errors of a parsed <paramref name="template"/>, or an empty list when it is valid.</summary>
    public static IReadOnlyList<ScribanTemplateError> GetErrors(Scriban.Template template)
    {
        if (!template.HasErrors)
        {
            return Array.Empty<ScribanTemplateError>();
        }

        // Scriban's positions are zero-based; an editor shows one-based ones.
        return template.Messages
            .Where(m => m.Type == ParserMessageType.Error)
            .Select(m => new ScribanTemplateError(m.Span.Start.Line + 1, m.Span.Start.Column + 1, m.Message))
            .ToList();
    }

    /// <summary>One <c>"(line,col): message"</c> per error, joined with a newline.</summary>
    public static string FormatErrors(IReadOnlyList<ScribanTemplateError> errors)
    {
        return string.Join("\n", errors.Select(e => $"({e.Line},{e.Column}): {e.Message}"));
    }

    /// <summary>
    /// Whether the parsed <paramref name="template"/> refers to <see cref="SiteThemeConsts.SiteHeadFunctionName"/>
    /// anywhere in its body - as a bare <c>{{ site_head }}</c> or a call <c>{{ site_head "title" }}</c>, both of
    /// which contain the global variable of that name. Walking the tree, not searching the text, so the word in plain
    /// markup or in a string does not count. A partial it includes is not followed; see the theme check for that.
    /// </summary>
    public static bool CallsSiteHead(Scriban.Template template)
    {
        return ContainsGlobal(template, SiteThemeConsts.SiteHeadFunctionName);
    }

    /// <summary>Whether the parsed <paramref name="template"/> reads <see cref="SiteThemeConsts.BodyVariableName"/>, as a layout should.</summary>
    public static bool UsesBody(Scriban.Template template)
    {
        return ContainsGlobal(template, SiteThemeConsts.BodyVariableName);
    }

    /// <summary>
    /// The front matter of the parsed <paramref name="template"/>: its <c>layout</c>, and an error for anything else
    /// in it. Only <c>layout = "path"</c> with a string literal is allowed - a value computed at run time could not
    /// be read without rendering, and an unknown key is refused so that a key added later cannot change what a
    /// template already saved means. <see cref="SiteTemplateFrontMatter.Empty"/> when there is no front matter, or
    /// when the template has syntax errors.
    /// </summary>
    public static SiteTemplateFrontMatter ReadFrontMatter(Scriban.Template template)
    {
        var frontMatter = template.Page?.FrontMatter;
        if (frontMatter == null || template.HasErrors)
        {
            return SiteTemplateFrontMatter.Empty;
        }

        SiteThemeReference? layout = null;
        var errors = new List<ScribanTemplateError>();

        foreach (var statement in frontMatter.Statements.Statements)
        {
            if (statement is ScriptNopStatement)
            {
                continue;
            }

            if (statement is ScriptExpressionStatement
                {
                    Expression: ScriptAssignExpression
                    {
                        Target: ScriptVariableGlobal { Name: SiteThemeConsts.LayoutKey },
                        Value: ScriptLiteral { Value: string value } literal
                    }
                })
            {
                if (layout != null)
                {
                    errors.Add(ToError(statement, $"'{SiteThemeConsts.LayoutKey}' is set more than once."));
                    continue;
                }

                layout = ToReference(SiteThemeConsts.LayoutKey, value, literal);
                continue;
            }

            errors.Add(ToError(
                statement,
                $"Front matter may only set {SiteThemeConsts.LayoutKey} = \"path\", with the path in quotes, " +
                $"or {SiteThemeConsts.LayoutKey} = \"{SiteThemeConsts.NoLayout}\"."));
        }

        return new SiteTemplateFrontMatter(true, layout, errors);
    }

    /// <summary>
    /// Every place the body of the parsed <paramref name="template"/> names another file with a string literal:
    /// <c>include "blog/_sidebar"</c>, <c>asset_url "css/site.css"</c>, and the same as a pipe
    /// (<c>"blog/_sidebar" | include</c>). A name computed at run time cannot be known without rendering and is not
    /// listed. The front matter's <c>layout</c> is not among them - see <see cref="ReadFrontMatter"/>. Empty for a
    /// template with syntax errors.
    /// </summary>
    public static IReadOnlyList<SiteThemeReference> GetReferences(Scriban.Template template)
    {
        var references = new List<SiteThemeReference>();

        if (template.Page?.Body != null && !template.HasErrors)
        {
            CollectReferences(template.Page.Body, references);
        }

        return references;
    }

    private static void CollectReferences(ScriptNode node, List<SiteThemeReference> references)
    {
        switch (node)
        {
            case ScriptFunctionCall call when call.Target is ScriptVariableGlobal target
                                              && IsReferencingFunction(target.Name)
                                              && call.Arguments.Count > 0
                                              && call.Arguments[0] is ScriptLiteral { Value: string path } literal:
                references.Add(ToReference(target.Name, path, literal, GetNamedArguments(call)));
                break;

            // The right side of a pipe is the bare variable or, as the parser usually builds it, a function call of
            // it without the piped argument.
            case ScriptPipeCall pipe when pipe.From is ScriptLiteral { Value: string pipedPath } pipedLiteral
                                          && GetPipedFunctionName(pipe.To) is { } pipedFunction
                                          && IsReferencingFunction(pipedFunction):
                references.Add(ToReference(
                    pipedFunction,
                    pipedPath,
                    pipedLiteral,
                    pipe.To is ScriptFunctionCall pipedCall ? GetNamedArguments(pipedCall) : null));
                break;
        }

        foreach (var child in node.Children)
        {
            if (child != null)
            {
                CollectReferences(child, references);
            }
        }
    }

    private static bool IsReferencingFunction(string name)
    {
        return name is SiteThemeConsts.IncludeFunctionName or SiteThemeConsts.AssetUrlFunctionName;
    }

    private static string? GetPipedFunctionName(ScriptExpression? to)
    {
        return to switch
        {
            ScriptVariableGlobal variable => variable.Name,
            ScriptFunctionCall { Target: ScriptVariableGlobal target } => target.Name,
            _ => null
        };
    }

    private static IReadOnlyList<string> GetNamedArguments(ScriptFunctionCall call)
    {
        return call.Arguments
            .OfType<ScriptNamedArgument>()
            .Select(argument => argument.Name.BaseName)
            .ToList();
    }

    /// <summary>
    /// The local variables the parsed <paramref name="template"/> reads without ever giving them a value, which an
    /// <c>include</c> has to pass as named arguments: <c>{{ $title }}</c> in a partial whose caller passed no
    /// <c>title:</c> fails the render, since the partial's locals are its own and nothing else sets them. A local
    /// the template assigns (<c>$x = ...</c>), loops over (<c>for $item in ...</c>) or captures counts as given, wherever
    /// in the template that happens, so the check errs toward saying nothing. Not counted: the positional
    /// arguments (<c>$1</c>), the loop state (<c>$for</c>), and anything inside a <c>func</c> or <c>do</c>, whose
    /// locals are the function's own. Empty for a template with syntax errors.
    /// </summary>
    public static IReadOnlyCollection<string> GetRequiredLocals(Scriban.Template template)
    {
        if (template.Page?.Body == null || template.HasErrors)
        {
            return Array.Empty<string>();
        }

        var read = new HashSet<string>(StringComparer.Ordinal);
        var given = new HashSet<string>(StringComparer.Ordinal);
        CollectLocals(template.Page.Body, read, given);

        read.ExceptWith(given);
        return read.OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    private static void CollectLocals(ScriptNode node, HashSet<string> read, HashSet<string> given)
    {
        switch (node)
        {
            case ScriptFunction:
                return;

            case ScriptAssignExpression { Target: ScriptVariableLocal target }:
                given.Add(target.BaseName);
                break;

            case ScriptForStatement { Variable: ScriptVariableLocal variable }:
                given.Add(variable.BaseName);
                break;

            case ScriptCaptureStatement { Target: ScriptVariableLocal captured }:
                given.Add(captured.BaseName);
                break;

            case ScriptVariableLocal local when IsNamedLocal(local):
                read.Add(local.BaseName);
                break;
        }

        foreach (var child in node.Children)
        {
            if (child != null)
            {
                CollectLocals(child, read, given);
            }
        }
    }

    /// <summary>A local a caller can pass: not <c>$</c> itself (the arguments, <c>$1</c> is an index of it) and not the loop state.</summary>
    private static bool IsNamedLocal(ScriptVariableLocal local)
    {
        return !string.IsNullOrEmpty(local.BaseName) && local.BaseName is not ("for" or "while" or "tablerow");
    }

    private static SiteThemeReference ToReference(
        string functionName,
        string path,
        ScriptNode literal,
        IReadOnlyList<string>? namedArguments = null)
    {
        var span = literal.Span;
        var normalized = path.Trim() == SiteThemeConsts.NoLayout && functionName == SiteThemeConsts.LayoutKey
            ? SiteThemeConsts.NoLayout
            : SiteThemePaths.Normalize(path);

        return new SiteThemeReference(
            functionName,
            normalized,
            span.Start.Line + 1,
            span.Start.Column + 1,
            span.Start.Offset,
            span.End.Offset - span.Start.Offset + 1,
            namedArguments);
    }

    private static ScribanTemplateError ToError(ScriptNode node, string message)
    {
        return new ScribanTemplateError(node.Span.Start.Line + 1, node.Span.Start.Column + 1, message);
    }

    private static bool ContainsGlobal(Scriban.Template template, string name)
    {
        return template.Page?.Body != null && ContainsGlobal(template.Page.Body, name);
    }

    private static bool ContainsGlobal(ScriptNode node, string name)
    {
        if (node is ScriptVariableGlobal variable && variable.Name == name)
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (child != null && ContainsGlobal(child, name))
            {
                return true;
            }
        }

        return false;
    }
}
