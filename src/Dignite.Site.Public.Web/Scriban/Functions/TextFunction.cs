using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>t key [fallback] [arguments...]</c>, also <c>{{ "Blog:Name" | t }}</c>: the theme's own text named <c>key</c>
/// (<c>locales/{culture}.json</c>, <see cref="SiteThemeTextCatalog"/>), in the page's language with the parent
/// languages and the site's default language behind it (<see cref="SiteTemplateCultureChain"/>) - or
/// <c>fallback</c>, or failing that the key itself, when the theme has not written it. Written HTML-encoded, like
/// every value; a text that holds markup on purpose is written with <c>{{ t "key" | html_raw }}</c>.
/// <para>
/// Arguments after the fallback fill the text's placeholders the way a Razor view's <c>L["key", args]</c> does,
/// with .NET composite formatting in the page's language: <c>"Page {0}"</c>, <c>"{0:yyyy年M月}の記事"</c> with a date,
/// <c>"{0:N0} 円"</c> with a number. Pass <c>null</c> as the fallback to give arguments without one:
/// <c>t "Pager:Page" null page_no</c>. At most <see cref="MaxArguments"/>.
/// </para>
/// <para>
/// The theme's texts only: the files a host ships for its Razor views (<see cref="SiteTemplateLocalizationContributor"/>)
/// are not read, so what a theme shows is what its release holds.
/// </para>
/// </summary>
public class TextFunction : SiteScriptFunction
{
    public const string Name = "t";

    /// <summary>The most arguments a text's placeholders can take.</summary>
    public const int MaxArguments = 10;

    /// <summary>What an argument the template did not pass arrives as, so that a <c>null</c> it did pass is kept.</summary>
    private static readonly object NotPassed = new();

    protected SiteThemeTextCatalog Texts { get; }

    protected IReadOnlyList<string> CultureChain { get; }

    /// <summary>The page's language, which formats the arguments.</summary>
    protected CultureInfo Culture { get; }

    /// <param name="texts">The texts of the theme being rendered.</param>
    /// <param name="cultureName">The page's language.</param>
    /// <param name="defaultCultureName">The site's default language, the last one tried; null for none.</param>
    public TextFunction(SiteThemeTextCatalog texts, string cultureName, string? defaultCultureName)
        : base(new[] { Required<string>("key"), Optional<string>("fallback") }
            .Concat(Enumerable.Range(0, MaxArguments).Select(i => Optional<object>("argument" + i, NotPassed)))
            .ToArray())
    {
        Texts = texts;
        CultureChain = SiteTemplateCultureChain.Create(cultureName, defaultCultureName);
        Culture = GetCulture(cultureName);
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var key = arguments[0] as string;
        if (string.IsNullOrEmpty(key))
        {
            return new ValueTask<object?>(string.Empty);
        }

        var text = Texts.Find(CultureChain, key) ?? arguments[1] as string ?? key;

        var formatArguments = arguments.Skip(2).TakeWhile(argument => !ReferenceEquals(argument, NotPassed)).ToArray();
        if (formatArguments.Length == 0)
        {
            return new ValueTask<object?>(text);
        }

        try
        {
            return new ValueTask<object?>(string.Format(Culture, text, formatArguments));
        }
        catch (FormatException exception)
        {
            throw new ScriptRuntimeException(span,
                $"The text '{key}' could not take {formatArguments.Length} argument(s): {exception.Message} The text is \"{text}\".");
        }
    }

    private static CultureInfo GetCulture(string cultureName)
    {
        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
