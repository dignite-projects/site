import { Injectable } from '@angular/core';
import type { GetContentListInput } from '../proxy/dignite/site/admin/contents/models';
import type { FlexFieldQueryCondition } from '../proxy/dignite/abp/flex-fields/models';

export interface ContentListState {
  filters: GetContentListInput;
  appliedConditions: FlexFieldQueryCondition[];
  /** Raw `flexFieldSearch` form value, keyed by field name - what each search widget should redisplay. */
  searchValues: Record<string, unknown>;
  page: number;
  sortKey: string | number;
  sortOrder: string;
  maxResultCount: number;
}

/**
 * Remembers the content list's filters, search conditions and paging across a round trip to the
 * create/edit page and back.
 *
 * `ContentsComponent` is destroyed on every route change - editing a content is a full page
 * (`content-editor.component.ts`), not a dialog - so without this, `filters`, the applied flex-field
 * search and the `ListService` it owns would all reset to their defaults every time an admin finishes
 * editing and returns to the list.
 *
 * In-memory rather than the URL: `flexFieldConditions` covers arbitrary per-content-type fields, and
 * round-tripping that shape through query params would need close to as much code as the search feature
 * itself. Losing the snapshot on a hard refresh is an acceptable trade for an admin filter, unlike a
 * shareable resource link.
 */
@Injectable({ providedIn: 'root' })
export class ContentListStateService {
  private snapshot?: ContentListState;

  save(state: ContentListState): void {
    this.snapshot = state;
  }

  restore(): ContentListState | undefined {
    return this.snapshot;
  }
}
