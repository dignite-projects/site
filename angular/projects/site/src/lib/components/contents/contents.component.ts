import {
  ABP,
  CoreModule,
  LIST_QUERY_DEBOUNCE_TIME,
  ListService,
  PagedResultDto,
  PermissionDirective,
} from '@abp/ng.core';
import {
  Confirmation,
  ConfirmationService,
  ThemeSharedModule,
  ToasterService,
} from '@abp/ng.theme.shared';
import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FlexFieldSearchComponent, FlexFieldViewComponent } from '@dignite/ng.flex-fields';
import type { FlexFieldData, FlexFieldValue } from '@dignite/ng.flex-fields';
import { Observable, forkJoin, map, of } from 'rxjs';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import { ContentTypeAdminService } from '../../proxy/dignite/site/admin/content-types/content-type-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import type { GetContentListInput } from '../../proxy/dignite/site/admin/contents/models';
import type { ContentTypeDto } from '../../proxy/dignite/site/content-types/models';
import type { ContentDto } from '../../proxy/dignite/site/contents/models';
import { ContentStatus } from '../../proxy/dignite/site/contents/content-status.enum';
import { FlexFieldQueryOperator } from '../../proxy/dignite/abp/flex-fields/flex-field-query-operator.enum';
import { FlexFieldValueType } from '../../proxy/dignite/abp/flex-fields/flex-field-value-type.enum';
import type { FlexFieldQueryCondition } from '../../proxy/dignite/abp/flex-fields/models';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { eSitePolicyNames } from '../../enums/policy-names';
import { ContentListStateService } from '../../services/content-list-state.service';
import { ContentQueryService } from '../../services/content-query.service';
import { SiteReferenceDataService } from '../../services/site-reference-data.service';
import type { ArrangedField, FieldColumn, RowFieldValues } from './arranged-field';

@Component({
  selector: 'site-contents',
  templateUrl: './contents.component.html',
  imports: [
    CoreModule,
    ThemeSharedModule,
    ReactiveFormsModule,
    PermissionDirective,
    RouterLink,
    FlexFieldSearchComponent,
    FlexFieldViewComponent,
  ],
  providers: [ListService, { provide: LIST_QUERY_DEBOUNCE_TIME, useValue: 300 }],
})
export class ContentsComponent {
  private readonly contentService = inject(ContentAdminService);
  private readonly contentQuery = inject(ContentQueryService);
  private readonly listState = inject(ContentListStateService);
  private readonly contentTypeService = inject(ContentTypeAdminService);
  private readonly pageService = inject(PageAdminService);
  private readonly referenceData = inject(SiteReferenceDataService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);

  readonly list = inject(ListService);
  readonly policies = eSitePolicyNames;
  readonly ContentStatus = ContentStatus;
  readonly statuses = [ContentStatus.Draft, ContentStatus.Published, ContentStatus.Archived];

  data: PagedResultDto<ContentDto> = { items: [], totalCount: 0 };
  filters = {} as GetContentListInput;

  pages: PageDto[] = [];
  languages: string[] = [];
  contentTypes: ContentTypeDto[] = [];

  /**
   * Every content type across every page, for resolving a row's display name. Deliberately separate
   * from {@link contentTypes}, which is scoped to the selected page and drives the filter dropdown and
   * field arrangements - a row's own page is not necessarily the one currently filtered on.
   */
  private contentTypesById = new Map<string, ContentTypeDto>();

  private fieldsById = new Map<string, FieldDto>();

  /** Each content type's arrangement, joined with the field library. Keyed by content type id. */
  private arrangementByContentType = new Map<string, ArrangedField[]>();

  /** Union of `showInList` fields across the page's content types, deduped by field. */
  columns: FieldColumn[] = [];

  /** Union of searchable fields, deduped by field. */
  searchFields: ArrangedField[] = [];

  // --- stable-reference caches --------------------------------------------------------------------
  //
  // `ff-flex-field-*` decide whether to re-render by comparing their `fields` input's *object
  // reference*. Binding `[fields]="someMethodCall(row, column)"` in a template calls that method - and
  // allocates a new object - on every change-detection pass. The child sees a changed input, tears
  // down and recreates its dynamic sub-component, which itself triggers change detection: Angular
  // reports `NG0103: Infinite change detection` and the page never finishes rendering. So both caches
  // hold stable references, rebuilt only when the underlying data actually changes.

  /** Per field definition, for the search bar. Keyed by field name. */
  private definitionValues = new Map<string, FlexFieldValue>();

  /** Per row then field name, for the list cells. */
  private rowValues = new Map<string, RowFieldValues>();

  readonly searchForm = this.fb.group({ flexFieldSearch: this.fb.group({}) });
  private appliedConditions: FlexFieldQueryCondition[] = [];

  /** Raw `flexFieldSearch` value restored from {@link ContentListStateService}, keyed by field name. */
  private searchValues: Record<string, unknown> = {};

  constructor() {
    const cached = this.listState.restore();
    if (cached) {
      this.filters = cached.filters;
      this.appliedConditions = cached.appliedConditions;
      this.searchValues = cached.searchValues;
      this.list.page = cached.page;
      this.list.sortKey = cached.sortKey;
      this.list.sortOrder = cached.sortOrder;
      this.list.maxResultCount = cached.maxResultCount;
    }

    this.pageService.getList({}).subscribe(result => {
      this.pages = result.items ?? [];
    });

    // Restores the content-type dropdown, columns and search fields for a restored `pageId` - a no-op
    // when there is none, same as a fresh visit.
    this.fetchContentTypes().subscribe();

    forkJoin({
      schema: this.referenceData.getSchema(),
      fields: this.referenceData.getFieldsById(),
      contentTypes: this.referenceData.getContentTypesById(),
    }).subscribe(({ schema, fields, contentTypes }) => {
      // The schema projection is name-keyed, so it is used only for the site-level settings; field
      // resolution goes through the Guid-keyed library below.
      this.languages = schema.enabledLanguages ?? [];
      this.filters.cultureName ??= schema.defaultLanguage ?? this.languages[0] ?? null;
      this.fieldsById = fields;
      this.contentTypesById = contentTypes;
      this.rebuildArrangements();
    });

    const getData = (query: ABP.PageQueryParams) => {
      // Snapshot on every real query, not just on `applyFilters()` - this is the one point every
      // filter, search and (future) paging change already funnels through.
      this.listState.save({
        filters: this.filters,
        appliedConditions: this.appliedConditions,
        searchValues: this.searchForm.get('flexFieldSearch')?.value ?? {},
        page: this.list.page,
        sortKey: this.list.sortKey,
        sortOrder: this.list.sortOrder,
        maxResultCount: this.list.maxResultCount,
      });

      return this.contentQuery.getList({
        ...query,
        ...this.filters,
        flexFieldConditions: this.appliedConditions,
      });
    };
    const setData = (result: PagedResultDto<ContentDto>) => {
      this.data = result;
      this.rebuildRowValues();
    };

    this.list.hookToQuery(getData).subscribe(setData);
  }

  // --- filters ------------------------------------------------------------------------------------

  onPageChange(): void {
    this.filters.contentTypeId = null;
    this.clearSearch();
    this.loadContentTypes();
  }

  applyFilters(): void {
    this.appliedConditions = this.buildConditions();
    this.list.page = 0;
    this.list.get();
  }

  /**
   * Resets each existing control rather than replacing the `flexFieldSearch` group.
   *
   * Every `ff-flex-field-search` keeps its own reference to the group it registered its control on
   * (`FieldTypeControlBase.valuesGroup`), captured when its inputs were first set. Swapping in a new
   * group would not change the identity of anything those children can see - `searchForm` itself is
   * unchanged - so none of them would re-register. They would keep writing into the old, detached
   * group while `buildConditions()` read the new, permanently empty one.
   */
  clearSearch(): void {
    const group = this.searchForm.get('flexFieldSearch') as FormGroup;
    Object.values(group.controls).forEach(control => control.reset(''));
    this.appliedConditions = [];
    this.searchValues = {};
  }

  clearFiltersAndSearch(): void {
    const cultureName = this.filters.cultureName;
    this.filters = { cultureName } as GetContentListInput;
    this.clearSearch();
    this.contentTypes = [];
    this.rebuildArrangements();
    this.applyFilters();
  }

  private loadContentTypes(): void {
    this.fetchContentTypes().subscribe(() => this.applyFilters());
  }

  /** Loads `contentTypes` for the current `filters.pageId` and rebuilds the derived arrangements. */
  private fetchContentTypes(): Observable<void> {
    if (!this.filters.pageId) {
      this.contentTypes = [];
      this.rebuildArrangements();
      return of<void>(undefined);
    }

    return this.contentTypeService.getListByPage(this.filters.pageId).pipe(
      map(result => {
        this.contentTypes = result.items ?? [];
        this.rebuildArrangements();
      }),
    );
  }

  // --- arrangement, columns, search fields ---------------------------------------------------------

  /**
   * Recomputes everything derived from "which content types are in play": the per-type arrangements,
   * the column union and the searchable union. Runs on page change and whenever the field library
   * arrives, never per change-detection pass.
   */
  private rebuildArrangements(): void {
    this.arrangementByContentType = new Map(
      this.contentTypes.map(contentType => [
        contentType.id!,
        (contentType.fields ?? [])
          .map(entry => this.toArrangedField(entry.fieldId, entry))
          .filter((field): field is ArrangedField => !!field)
          .sort((a, b) => a.order - b.order),
      ]),
    );

    this.rebuildColumns();
    this.rebuildSearchFields();
    this.rebuildRowValues();
  }

  private toArrangedField(
    fieldId: string | undefined,
    usage: { required?: boolean; searchable?: boolean; showInList?: boolean; displayName?: string | null; order?: number },
  ): ArrangedField | undefined {
    const definition = fieldId ? this.fieldsById.get(fieldId) : undefined;

    if (!definition) {
      return undefined;
    }

    const fieldData: FlexFieldData = {
      id: definition.id!,
      name: definition.name!,
      displayName: usage.displayName || definition.displayName!,
      description: definition.description ?? undefined,
      fieldTypeName: definition.fieldTypeName!,
      configuration: (definition.configuration ?? {}) as Record<string, unknown>,
    };

    return {
      fieldId: definition.id!,
      name: definition.name!,
      displayName: fieldData.displayName,
      fieldTypeName: definition.fieldTypeName!,
      required: !!usage.required,
      searchable: !!usage.searchable,
      showInList: !!usage.showInList,
      order: usage.order ?? 0,
      fieldData,
    };
  }

  /**
   * A field earns a column when a content type marks it `showInList` - that flag is the whole answer.
   *
   * No capability check on top of it: whether a field type ships a view component is not a reason to
   * drop a column the arrangement explicitly asked for. Silently omitting it would leave the editor
   * looking at a setting that appears to do nothing.
   */
  private rebuildColumns(): void {
    const columns = new Map<string, FieldColumn>();

    for (const [contentTypeId, fields] of this.arrangementByContentType) {
      for (const field of fields) {
        if (!field.showInList) {
          continue;
        }

        const existing = columns.get(field.fieldId);
        if (existing) {
          existing.contentTypeIds.add(contentTypeId);
        } else {
          columns.set(field.fieldId, {
            fieldId: field.fieldId,
            name: field.name,
            displayName: field.displayName,
            fieldTypeName: field.fieldTypeName,
            contentTypeIds: new Set([contentTypeId]),
          });
        }
      }
    }

    this.columns = [...columns.values()];
  }

  /**
   * A field is filterable when a content type marks it `searchable` - again, that flag is the whole
   * answer. The field arrangement editor already refuses to set it on a type the server reports as
   * non-indexable, so the check belongs there, where it can explain itself, rather than here where it
   * would quietly drop a filter someone had configured.
   *
   * `ff-flex-field-search` renders nothing for a type with no search component (`DateTime` today); that
   * is the library's documented behaviour, not something to pre-empt by hiding the field.
   */
  private rebuildSearchFields(): void {
    const fields = new Map<string, ArrangedField>();

    for (const arranged of this.arrangementByContentType.values()) {
      for (const field of arranged) {
        if (fields.has(field.fieldId) || !field.searchable) {
          continue;
        }
        fields.set(field.fieldId, field);
      }
    }

    this.searchFields = [...fields.values()];
    this.definitionValues = new Map(
      this.searchFields.map(field => [
        field.name,
        { field: field.fieldData, required: field.required, searchable: true, value: undefined },
      ]),
    );
  }

  private rebuildRowValues(): void {
    this.rowValues = new Map(
      (this.data.items ?? []).map(content => [
        content.id!,
        new Map(
          (this.arrangementByContentType.get(content.contentTypeId ?? '') ?? []).map(field => [
            field.name,
            {
              field: field.fieldData,
              required: field.required,
              searchable: field.searchable,
              value: content.fieldValues?.[field.name],
            } as FlexFieldValue,
          ]),
        ),
      ]),
    );
  }

  /** The stable value for one cell, or `undefined` when this row's type does not use the field. */
  rowValueOf(content: ContentDto, column: FieldColumn): FlexFieldValue | undefined {
    return this.rowValues.get(content.id!)?.get(column.name);
  }

  /** The stable value for one search widget. See {@link definitionValues}. */
  definitionValueOf(field: ArrangedField): FlexFieldValue | undefined {
    return this.definitionValues.get(field.name);
  }

  /** What one search widget should redisplay on restore - see {@link ContentListStateService}. */
  selectedValueOf(field: ArrangedField): unknown {
    return this.searchValues[field.name] ?? '';
  }

  // --- search conditions ---------------------------------------------------------------------------

  /**
   * Turns what each `ff-flex-field-search` wrote into the conditions the server understands. The
   * mapping depends on each field type's storage shape, not on anything generic the search form knows.
   */
  private buildConditions(): FlexFieldQueryCondition[] {
    const raw = (this.searchForm.get('flexFieldSearch')?.value ?? {}) as Record<string, unknown>;
    return this.searchFields.flatMap(field => this.toConditions(field, raw[field.name]));
  }

  private toConditions(field: ArrangedField, value: unknown): FlexFieldQueryCondition[] {
    switch (field.fieldTypeName) {
      case 'Text':
        return this.isBlank(value)
          ? []
          : [
              this.condition(
                field,
                FlexFieldQueryOperator.Contains,
                String(value),
                FlexFieldValueType.String,
              ),
            ];

      case 'Number': {
        // `ff-number-search` writes one "min-max" string; the executor composes two conditions on the
        // same field as a range rather than a conflict.
        const range = typeof value === 'string' ? value.split('-') : [];
        if (range.length !== 2 || range.some(part => part === '')) {
          return [];
        }
        return [
          this.condition(
            field,
            FlexFieldQueryOperator.GreaterThanOrEqual,
            range[0],
            FlexFieldValueType.Number,
          ),
          this.condition(
            field,
            FlexFieldQueryOperator.LessThanOrEqual,
            range[1],
            FlexFieldValueType.Number,
          ),
        ];
      }

      case 'Boolean':
        // A plain `<option [value]>` binding always stringifies, so this arrives as "true"/"false".
        return value === 'true' || value === 'false'
          ? [this.condition(field, FlexFieldQueryOperator.Equals, value, FlexFieldValueType.Boolean)]
          : [];

      case 'Select':
      case 'Tree': {
        const values = Array.isArray(value) ? value : this.isBlank(value) ? [] : [value];
        return values.length === 0
          ? []
          : [
              this.condition(
                field,
                FlexFieldQueryOperator.In,
                values.join(','),
                FlexFieldValueType.String,
              ),
            ];
      }

      default:
        return [];
    }
  }

  /** Both `fieldId` and `fieldName` are required by the server - always send the pair. */
  private condition(
    field: ArrangedField,
    operator: FlexFieldQueryOperator,
    value: string,
    valueType: FlexFieldValueType,
  ): FlexFieldQueryCondition {
    return { fieldId: field.fieldId, fieldName: field.name, operator, value, valueType };
  }

  private isBlank(value: unknown): boolean {
    return value === null || value === undefined || value === '';
  }

  // --- display helpers -----------------------------------------------------------------------------

  statusKeyOf(status?: ContentStatus): string {
    return `Site::Enum:ContentStatus:${ContentStatus[status ?? ContentStatus.Draft]}`;
  }

  contentTypeNameOf(contentTypeId?: string): string {
    return (contentTypeId ? this.contentTypesById.get(contentTypeId)?.displayName : undefined) ?? '---';
  }

  pageNameOf(pageId?: string): string {
    return this.pages.find(page => page.id === pageId)?.displayName ?? '---';
  }

  // --- create / edit -------------------------------------------------------------------------------
  //
  // Editing happens on its own page (`/site/contents/create`, `/site/contents/:id/edit`), so this
  // component only links there. Deleting stays here because it needs no form.

  /** Creating needs a page picked, since a content type belongs to one. */
  get canCreate(): boolean {
    return !!this.filters.pageId && this.contentTypes.length > 0;
  }

  delete(content: ContentDto): void {
    this.confirmation
      .warn('AbpUi::ItemWillBeDeletedMessage', 'AbpUi::AreYouSure', {
        messageLocalizationParams: [content.slug || this.pageNameOf(content.pageId)],
      })
      .subscribe((status: Confirmation.Status) => {
        if (status === Confirmation.Status.confirm) {
          this.contentService.delete(content.id!).subscribe(() => {
            this.list.get();
            this.toaster.success('AbpUi::SuccessfullyDeleted');
          });
        }
      });
  }
}
