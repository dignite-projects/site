import type { GetContentListInput } from '../proxy/dignite/site/admin/contents/models';

/**
 * Flattens a content-list query into the shape ASP.NET Core's model binder actually reads.
 *
 * `GET /api/site-admin/contents` binds `GetContentListInput` from the query string, and that input
 * carries `FlexFieldConditions: List<FlexFieldQueryCondition>` - a collection of complex objects.
 * ASP.NET Core binds those only from indexed keys (`flexFieldConditions[0].fieldId=...`), whereas the
 * generated proxy hands the array straight to `RestService`, which builds an `HttpParams` via
 * `{ fromObject }`. Angular stringifies each element, so the server receives
 * `flexFieldConditions=[object Object]` and every condition is silently dropped - no error, just
 * unfiltered results. Hence this translation rather than using the generated `getList`.
 *
 * The flex-fields demo does not need an equivalent because its list endpoint is a POST with a body
 * (`/api/app/product/search`); Site's is a GET, so the conditions have to survive the query string.
 */
export function toContentListParams(input: GetContentListInput): Record<string, unknown> {
  const { flexFieldConditions, ...rest } = input;
  const params: Record<string, unknown> = { ...rest };

  (flexFieldConditions ?? []).forEach((condition, index) => {
    const prefix = `flexFieldConditions[${index}]`;
    params[`${prefix}.fieldId`] = condition.fieldId;
    params[`${prefix}.fieldName`] = condition.fieldName;
    params[`${prefix}.operator`] = condition.operator;
    params[`${prefix}.value`] = condition.value;
    params[`${prefix}.valueType`] = condition.valueType;
  });

  return params;
}
