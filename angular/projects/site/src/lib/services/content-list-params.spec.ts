import type { GetContentListInput } from '../proxy/dignite/site/admin/contents/models';
import { toContentListParams } from './content-list-params';

describe('toContentListParams', () => {
  it('passes non-flexFieldConditions fields through untouched', () => {
    const input: GetContentListInput = {
      pageId: 'page-1',
      filter: 'hello',
      maxResultCount: 20,
    };

    expect(toContentListParams(input)).toEqual({
      pageId: 'page-1',
      filter: 'hello',
      maxResultCount: 20,
    });
  });

  it('flattens a single condition into indexed keys', () => {
    const input: GetContentListInput = {
      maxResultCount: 10,
      flexFieldConditions: [
        { fieldId: 'f1', fieldName: 'title', operator: 'Equals' as any, value: 'foo', valueType: 'String' as any },
      ],
    };

    expect(toContentListParams(input)).toEqual({
      maxResultCount: 10,
      'flexFieldConditions[0].fieldId': 'f1',
      'flexFieldConditions[0].fieldName': 'title',
      'flexFieldConditions[0].operator': 'Equals',
      'flexFieldConditions[0].value': 'foo',
      'flexFieldConditions[0].valueType': 'String',
    });
  });

  it('indexes multiple conditions in order and drops the array field itself', () => {
    const input: GetContentListInput = {
      maxResultCount: 10,
      filter: 'unrelated',
      flexFieldConditions: [
        { fieldId: 'f1', fieldName: 'a' },
        { fieldId: 'f2', fieldName: 'b' },
      ],
    };

    const params = toContentListParams(input);

    expect(params['filter']).toBe('unrelated');
    expect(params['flexFieldConditions[0].fieldName']).toBe('a');
    expect(params['flexFieldConditions[1].fieldName']).toBe('b');
    // The raw array must never reach RestService - that's the whole bug this function fixes.
    expect(params['flexFieldConditions']).toBeUndefined();
  });

  it('produces no flexFieldConditions keys when the list is undefined', () => {
    const params = toContentListParams({ maxResultCount: 10, filter: 'x' });

    expect(Object.keys(params).some(key => key.startsWith('flexFieldConditions'))).toBe(false);
  });

  it('produces no flexFieldConditions keys when the list is empty', () => {
    const params = toContentListParams({ maxResultCount: 10, flexFieldConditions: [] });

    expect(params).toEqual({ maxResultCount: 10 });
  });
});
