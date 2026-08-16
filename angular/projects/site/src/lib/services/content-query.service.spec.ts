import { RestService } from '@abp/ng.core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import type { GetContentListInput } from '../proxy/dignite/site/admin/contents/models';
import { ContentQueryService } from './content-query.service';

describe('ContentQueryService', () => {
  const restService = {
    request: vi.fn(() => of({ items: [], totalCount: 0 })),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [{ provide: RestService, useValue: restService }],
    });
  });

  it('requests the content list endpoint with the flattened params and the SiteAdmin api', () => {
    const service = TestBed.inject(ContentQueryService);
    const input: GetContentListInput = {
      pageId: 'page-1',
      maxResultCount: 10,
      flexFieldConditions: [{ fieldId: 'f1', fieldName: 'title', value: 'foo' }],
    };

    service.getList(input);

    expect(restService.request).toHaveBeenCalledWith(
      {
        method: 'GET',
        url: '/api/site-admin/contents',
        params: {
          pageId: 'page-1',
          maxResultCount: 10,
          'flexFieldConditions[0].fieldId': 'f1',
          'flexFieldConditions[0].fieldName': 'title',
          'flexFieldConditions[0].operator': undefined,
          'flexFieldConditions[0].value': 'foo',
          'flexFieldConditions[0].valueType': undefined,
        },
      },
      { apiName: 'SiteAdmin' },
    );
  });

  it('merges caller-supplied config with apiName rather than replacing it', () => {
    const service = TestBed.inject(ContentQueryService);

    service.getList({ maxResultCount: 10 }, { observe: 'response' as any });

    expect(restService.request).toHaveBeenCalledWith(expect.anything(), {
      apiName: 'SiteAdmin',
      observe: 'response',
    });
  });
});
