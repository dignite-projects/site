using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Http.Client.ClientProxying;
using Volo.Abp.Http.Modeling;

namespace Dignite.Site.Common;

/// <summary>
/// Writes a list of <see cref="FlexFieldQueryCondition"/>s - the <c>FlexFieldConditions</c> of both the
/// Public and the Admin <c>GetContentListInput</c> - into the query string as one indexed group of keys
/// per condition, <c>FlexFieldConditions[0].FieldId=...&amp;FlexFieldConditions[0].Operator=Equals&amp;...</c>,
/// the form ASP.NET Core model binding reads back into the list.
/// <para>
/// ABP's <c>ClientProxyUrlBuilder</c> has no such form of its own: it writes each list item as
/// <c>FlexFieldConditions[0]=</c> plus the item's <c>ToString()</c> - for an object, its type name - which
/// binds to nothing. Every condition was dropped on the way and the list came back unfiltered, with no
/// error on either side. Only a host that calls Site over HTTP builds this URL at all; one that calls the
/// app service in-process never did, which is how it went unnoticed.
/// </para>
/// <para>
/// Registered in <see cref="SiteCommonHttpApiClientModule"/>, which the Public and the Admin client
/// modules both depend on.
/// </para>
/// </summary>
public class FlexFieldQueryConditionListToQueryStringConverter
    : IObjectToQueryString<List<FlexFieldQueryCondition>>, ITransientDependency
{
    public virtual Task<string> ConvertAsync(
        ActionApiDescriptionModel actionApiDescription,
        ParameterApiDescriptionModel parameterApiDescription,
        List<FlexFieldQueryCondition> value)
    {
        var queryString = new StringBuilder();
        for (var i = 0; i < value.Count; i++)
        {
            var condition = value[i];
            var prefix = $"{parameterApiDescription.Name}[{i}].";
            Append(queryString, prefix + nameof(FlexFieldQueryCondition.FieldId), condition.FieldId.ToString());
            Append(queryString, prefix + nameof(FlexFieldQueryCondition.FieldName), condition.FieldName);
            Append(queryString, prefix + nameof(FlexFieldQueryCondition.Operator), condition.Operator.ToString());
            Append(queryString, prefix + nameof(FlexFieldQueryCondition.Value), condition.Value);
            Append(queryString, prefix + nameof(FlexFieldQueryCondition.ValueType), condition.ValueType.ToString());
        }

        // Null rather than empty for an empty list: ClientProxyUrlBuilder adds a separator for any
        // non-null result, and skips the parameter altogether for null.
        return Task.FromResult(queryString.Length == 0 ? null! : queryString.ToString());
    }

    private static void Append(StringBuilder queryString, string name, string? value)
    {
        if (queryString.Length > 0)
        {
            queryString.Append('&');
        }

        queryString.Append(name).Append('=').Append(WebUtility.UrlEncode(value ?? string.Empty));
    }
}
