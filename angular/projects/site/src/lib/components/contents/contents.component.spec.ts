import { TestBed } from '@angular/core/testing';
import { ListService } from '@abp/ng.core';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { of } from 'rxjs';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import { ContentTypeAdminService } from '../../proxy/dignite/site/admin/content-types/content-type-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import { FlexFieldQueryOperator } from '../../proxy/dignite/abp/flex-fields/flex-field-query-operator.enum';
import { FlexFieldValueType } from '../../proxy/dignite/abp/flex-fields/flex-field-value-type.enum';
import { ContentListStateService } from '../../services/content-list-state.service';
import { ContentQueryService } from '../../services/content-query.service';
import { SiteReferenceDataService } from '../../services/site-reference-data.service';
import { ContentsComponent } from './contents.component';
import type { ArrangedField } from './arranged-field';

describe('ContentsComponent.toConditions', () => {
  const referenceData = {
    getSchema: vi.fn(() => of({ enabledLanguages: ['en'] })),
    getFieldsById: vi.fn(() => of(new Map())),
    getContentTypesById: vi.fn(() => of(new Map())),
  };
  const pageService = { getList: vi.fn(() => of({ items: [] })) };
  const listState = { restore: vi.fn(() => undefined), save: vi.fn() };
  const list = { hookToQuery: vi.fn(() => of({ items: [], totalCount: 0 })), get: vi.fn(), page: 0 };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        ContentsComponent,
        { provide: ContentAdminService, useValue: {} },
        { provide: ContentTypeAdminService, useValue: {} },
        { provide: PageAdminService, useValue: pageService },
        { provide: SiteReferenceDataService, useValue: referenceData },
        { provide: ContentQueryService, useValue: {} },
        { provide: ContentListStateService, useValue: listState },
        { provide: ConfirmationService, useValue: {} },
        { provide: ToasterService, useValue: {} },
        { provide: ListService, useValue: list },
      ],
    });
  });

  /** A minimal, valid `ArrangedField` - only `fieldTypeName` varies per case below. */
  function field(fieldTypeName: string): ArrangedField {
    return {
      fieldId: 'field-1',
      name: 'the-field',
      displayName: 'The Field',
      fieldTypeName,
      required: false,
      searchable: true,
      showInList: false,
      order: 0,
      fieldData: {
        id: 'field-1',
        name: 'the-field',
        displayName: 'The Field',
        fieldTypeName,
        configuration: {},
      },
    };
  }

  /**
   * Calls the private `toConditions()` directly instead of through `applyFilters()`'s full
   * constructor-and-service wiring - what matters here is the field-type -> condition mapping, the
   * exact thing that went silently stale once already when FlexFields' registration keys were renamed
   * (`TextEdit`/`NumericEdit`/`Switch`/`TreeView` -> `Text`/`Number`/`Boolean`/`Tree`, 2026-08-17). This
   * mirrors why flex-fields itself keeps `built-in-field-types.spec.ts` on the kernel side.
   */
  function toConditions(fieldTypeName: string, value: unknown) {
    const component = TestBed.inject(ContentsComponent);
    return (component as any).toConditions(field(fieldTypeName), value);
  }

  it('matches a Text field with a Contains condition', () => {
    expect(toConditions('Text', 'hello')).toEqual([
      {
        fieldId: 'field-1',
        fieldName: 'the-field',
        operator: FlexFieldQueryOperator.Contains,
        value: 'hello',
        valueType: FlexFieldValueType.String,
      },
    ]);
  });

  it('ignores a blank Text value', () => {
    expect(toConditions('Text', '')).toEqual([]);
  });

  it('splits a Number range into two bounding conditions', () => {
    expect(toConditions('Number', '5-10')).toEqual([
      {
        fieldId: 'field-1',
        fieldName: 'the-field',
        operator: FlexFieldQueryOperator.GreaterThanOrEqual,
        value: '5',
        valueType: FlexFieldValueType.Number,
      },
      {
        fieldId: 'field-1',
        fieldName: 'the-field',
        operator: FlexFieldQueryOperator.LessThanOrEqual,
        value: '10',
        valueType: FlexFieldValueType.Number,
      },
    ]);
  });

  it('ignores an incomplete Number range', () => {
    expect(toConditions('Number', '5-')).toEqual([]);
  });

  it('reads a Boolean value from the stringified select option', () => {
    expect(toConditions('Boolean', 'true')).toEqual([
      {
        fieldId: 'field-1',
        fieldName: 'the-field',
        operator: FlexFieldQueryOperator.Equals,
        value: 'true',
        valueType: FlexFieldValueType.Boolean,
      },
    ]);
  });

  it('ignores a Boolean value that is neither "true" nor "false"', () => {
    expect(toConditions('Boolean', '')).toEqual([]);
  });

  it('joins a Select selection into one In condition', () => {
    expect(toConditions('Select', ['travel', 'food'])).toEqual([
      {
        fieldId: 'field-1',
        fieldName: 'the-field',
        operator: FlexFieldQueryOperator.In,
        value: 'travel,food',
        valueType: FlexFieldValueType.String,
      },
    ]);
  });

  it('joins a Tree selection into one In condition', () => {
    expect(toConditions('Tree', ['node-a'])).toEqual([
      {
        fieldId: 'field-1',
        fieldName: 'the-field',
        operator: FlexFieldQueryOperator.In,
        value: 'node-a',
        valueType: FlexFieldValueType.String,
      },
    ]);
  });

  it('ignores an empty Select/Tree selection', () => {
    expect(toConditions('Select', [])).toEqual([]);
  });

  it('contributes nothing for a field type with no search mapping, e.g. DateTime', () => {
    expect(toConditions('DateTime', 'anything')).toEqual([]);
  });
});
