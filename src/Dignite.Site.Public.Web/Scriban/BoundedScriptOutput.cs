using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// A template's output, refused past <see cref="MaxLength"/> characters - see <see cref="ScribanSandbox"/>. An
/// exception rather than truncation: a page cut off mid-tag is a broken page nobody is told about.
/// </summary>
public class BoundedScriptOutput : IScriptOutput
{
    private readonly StringBuilder _builder = new();

    public BoundedScriptOutput(string templateName, int maxLength)
    {
        TemplateName = templateName;
        MaxLength = maxLength;
    }

    /// <summary>The template writing to this output, for the error message.</summary>
    public string TemplateName { get; }

    public int MaxLength { get; }

    public int Length => _builder.Length;

    public void Write(string text, int offset, int count)
    {
        if (count <= 0)
        {
            return;
        }

        if ((long)_builder.Length + count > MaxLength)
        {
            throw new InvalidOperationException(
                $"The Scriban template '{TemplateName}' wrote more than {MaxLength} characters, the limit set by " +
                $"{nameof(SiteScribanOptions)}.{nameof(SiteScribanOptions.MaxOutputLength)}.");
        }

        _builder.Append(text, offset, count);
    }

    public ValueTask WriteAsync(string text, int offset, int count, CancellationToken cancellationToken)
    {
        Write(text, offset, count);
        return default;
    }

    /// <summary>Everything written so far - what <c>Template.RenderAsync</c> returns.</summary>
    public override string ToString()
    {
        return _builder.ToString();
    }
}
