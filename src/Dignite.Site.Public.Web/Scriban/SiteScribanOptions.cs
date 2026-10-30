using System;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// The resource limits a tenant's Scriban template renders under - see <see cref="ScribanSandbox"/> for what
/// each one defends against. A host tightens or widens them with <c>Configure&lt;SiteScribanOptions&gt;</c>.
/// </summary>
public class SiteScribanOptions
{
    /// <summary>
    /// Wall-clock budget for rendering one page, layout included. Default: 3 seconds. Checked by the engine
    /// between statements and on every loop iteration; a service call a template function is awaiting runs to
    /// completion first.
    /// </summary>
    public TimeSpan RenderTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Upper bound, in characters, on what one template (the page template, or the layout) may write.
    /// Default: 2 Mi characters.
    /// </summary>
    public int MaxOutputLength { get; set; } = 2 * 1024 * 1024;

    /// <summary>Per-match timeout for the <c>regex.*</c> functions. Default: 100 ms.</summary>
    public TimeSpan RegexTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum size of one range, and maximum iterations of one outermost loop - Scriban counts the iterations
    /// of every loop nested inside it towards the same limit, so a list of 100 items with ten fields each already
    /// spends 1000. Default: 10000 (Scriban's own default is 1000, too tight under that counting); the CPU bound is
    /// <see cref="RenderTimeout"/>. 0 disables it.
    /// </summary>
    public int LoopLimit { get; set; } = 10000;

    /// <summary>Maximum depth of nested function calls. Default: 100 (Scriban's own).</summary>
    public int RecursiveLimit { get; set; } = 100;

    /// <summary>Maximum depth the engine follows when turning a nested object into text. Default: 20 (Scriban's own).</summary>
    public int ObjectRecursionLimit { get; set; } = 20;

    /// <summary>
    /// Size limit of <see cref="ScribanTemplateCache"/>, counted in characters of template source (the parsed
    /// tree takes a multiple of that in memory). Default: 16 Mi characters.
    /// </summary>
    public long TemplateCacheSizeLimit { get; set; } = 16L * 1024 * 1024;
}
