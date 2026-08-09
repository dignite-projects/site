import { Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { GetContentListInput } from '../proxy/dignite/site/admin/contents/models';
import type { ContentDto } from '../proxy/dignite/site/contents/models';
import { toContentListParams } from './content-list-params';

/**
 * Stands in for `ContentAdminService.getList` alone. Everything else on the generated proxy is used
 * directly - only the list call needs `flexFieldConditions` flattened before it reaches `RestService`
 * (see {@link toContentListParams}). Kept here rather than patched into `proxy/` so that re-running
 * `abp generate-proxy` cannot quietly revert the fix.
 */
@Injectable({ providedIn: 'root' })
export class ContentQueryService {
  private readonly restService = inject(RestService);

  readonly apiName = 'SiteAdmin';

  getList = (input: GetContentListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<unknown, PagedResultDto<ContentDto>>(
      {
        method: 'GET',
        url: '/api/site-admin/contents',
        params: toContentListParams(input),
      },
      { apiName: this.apiName, ...config },
    );
}
