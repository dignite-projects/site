using System;
using System.Collections.Generic;
using System.Linq;
using Scriban;
using Scriban.Parsing;
using Scriban.Syntax;

namespace Dignite.Site.Templates;

/// <summary>
/// Parsing and static checks for tenant-authored Scriban templates. Lives in Domain.Shared, next to the
/// package reference, because the syntax check at save time and the renderer must share one parser: a
/// template that saves has to be a template that renders, and Public.Web may reference nothing but
/// Domain.Shared.
/// </summary>
public static class ScribanTemplateSyntax
{
    /// <summary>
    /// Parses <paramref name="body"/> with Scriban's default (Scriban, not Liquid) options. The result may
    /// have errors - see <see cref="GetErrors"/> - in which case its AST must not be relied on.
    /// </summary>
    public static Scriban.Template Parse(string body, string? sourceFilePath = null)
    {
        return Scriban.Template.Parse(body ?? string.Empty, sourceFilePath);
    }

    /// <summary>The syntax errors of <paramref name="body"/>, or an empty list when it is valid.</summary>
    public static IReadOnlyList<ScribanTemplateError> GetErrors(string body)
    {
        var template = Parse(body);
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

    /// <summary>
    /// Whether the parsed <paramref name="template"/> refers to <see cref="SiteTemplateConsts.SiteHeadFunctionName"/>
    /// anywhere in its syntax tree - as a bare <c>{{ site_head }}</c> or a call <c>{{ site_head "title" }}</c>,
    /// both of which contain the global variable of that name. Walking the tree, not searching the text, so
    /// the word in plain markup or in a string does not count.
    /// </summary>
    public static bool CallsSiteHead(Scriban.Template template)
    {
        return template.Page != null && ContainsSiteHead(template.Page);
    }

    /// <summary>One <c>"(line,col): message"</c> per error, joined with a newline.</summary>
    public static string FormatErrors(IReadOnlyList<ScribanTemplateError> errors)
    {
        return string.Join("\n", errors.Select(e => $"({e.Line},{e.Column}): {e.Message}"));
    }

    private static bool ContainsSiteHead(ScriptNode node)
    {
        if (node is ScriptVariableGlobal variable &&
            variable.Name == SiteTemplateConsts.SiteHeadFunctionName)
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (child != null && ContainsSiteHead(child))
            {
                return true;
            }
        }

        return false;
    }
}
