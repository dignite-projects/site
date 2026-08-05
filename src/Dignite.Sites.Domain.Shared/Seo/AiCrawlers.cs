using System.Collections.Generic;

namespace Dignite.Sites.Seo;

/// <summary>
/// The AI crawler product tokens <c>robots.txt</c> is generated for (总体设计 §5.6, GitHub issue #18).
/// <para>
/// Split into two classes because site owners routinely want opposite answers for them: "do not train on
/// my writing" and "do cite me in AI answers" are a coherent pair, and a single on/off switch cannot
/// express it. Each token gets its own <c>User-agent</c> group so the two decisions stay independent -
/// and so <c>Google-Extended</c>, which is a Gemini/Vertex training opt-out token rather than a crawler,
/// can be denied without touching Googlebot. Do not merge these into the <c>*</c> group.
/// </para>
/// </summary>
public static class AiCrawlers
{
    /// <summary>Crawlers that collect content to train models.</summary>
    public static IReadOnlyList<string> Training { get; } = new[]
    {
        "GPTBot",
        "Google-Extended",
        "ClaudeBot",
        "CCBot"
    };

    /// <summary>
    /// Crawlers that fetch content to answer a user's question and cite the source - the AI equivalent of
    /// a search index, and the reason blocking everything indiscriminately costs a site its visibility.
    /// </summary>
    public static IReadOnlyList<string> Search { get; } = new[]
    {
        "OAI-SearchBot",
        "PerplexityBot",
        "Claude-SearchBot"
    };
}
