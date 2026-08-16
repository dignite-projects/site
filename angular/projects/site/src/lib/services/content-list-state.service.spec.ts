import { TestBed } from '@angular/core/testing';
import { ContentListState, ContentListStateService } from './content-list-state.service';

describe('ContentListStateService', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  const sample: ContentListState = {
    filters: { pageId: 'page-1', maxResultCount: 10 },
    appliedConditions: [{ fieldId: 'f1', fieldName: 'title' }],
    searchValues: { title: 'foo' },
    page: 2,
    sortKey: 'creationTime',
    sortOrder: 'desc',
    maxResultCount: 10,
  };

  it('has nothing to restore before anything is saved', () => {
    const service = TestBed.inject(ContentListStateService);

    expect(service.restore()).toBeUndefined();
  });

  it('restores exactly what was saved', () => {
    const service = TestBed.inject(ContentListStateService);

    service.save(sample);

    expect(service.restore()).toEqual(sample);
  });

  it('overwrites the previous snapshot on a second save', () => {
    const service = TestBed.inject(ContentListStateService);
    service.save(sample);

    const next: ContentListState = { ...sample, page: 3, filters: { pageId: 'page-2', maxResultCount: 10 } };
    service.save(next);

    expect(service.restore()).toEqual(next);
  });

  it('is a singleton, so a save from one injection site is visible from another', () => {
    const first = TestBed.inject(ContentListStateService);
    first.save(sample);

    const second = TestBed.inject(ContentListStateService);

    expect(second.restore()).toEqual(sample);
  });
});
