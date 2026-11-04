using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Keeps MVC from reading the request body as a form before the action does. The body of an upload link is the file
/// itself, whatever the client says it is: <c>curl --data-binary</c> sends it as <c>application/x-www-form-urlencoded</c>
/// unless told otherwise, and the form value providers would then try to parse the bytes as fields - failing on them,
/// or, when they could, taking the body away from the action that stores it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var factories = context.ValueProviderFactories;

        foreach (var factory in factories
                     .Where(factory => factory is FormValueProviderFactory or JQueryFormValueProviderFactory)
                     .ToList())
        {
            factories.Remove(factory);
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
