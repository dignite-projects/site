namespace Dignite.Site.Seo;

/// <summary>One node of a resolved route's BreadcrumbList, home first (总体设计 §5.4, GitHub issue #20).</summary>
public class JsonLdBreadcrumbItem
{
    public JsonLdBreadcrumbItem(string name, string url)
    {
        Name = name;
        Url = url;
    }

    public string Name { get; }

    public string Url { get; }
}
