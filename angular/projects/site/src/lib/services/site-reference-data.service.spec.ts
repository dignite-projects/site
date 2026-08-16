import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ContentTypeAdminService } from '../proxy/dignite/site/admin/content-types/content-type-admin.service';
import { FieldAdminService } from '../proxy/dignite/site/admin/fields/field-admin.service';
import { SiteSchemaAdminService } from '../proxy/dignite/site/admin/schema/site-schema-admin.service';
import { SiteReferenceDataService } from './site-reference-data.service';

describe('SiteReferenceDataService', () => {
  const schemaService = { get: vi.fn(() => of({ enabledLanguages: ['en'] })) };
  const fieldService = {
    getList: vi.fn(() =>
      of({
        items: [
          { id: 'field-1', name: 'title' },
          { id: 'field-2', name: 'seo' },
        ],
      }),
    ),
    getFieldTypes: vi.fn(() =>
      of({
        items: [
          { name: 'TextEdit', indexable: true },
          { name: 'Seo', indexable: false },
          { name: 'NoAnswer' },
        ],
      }),
    ),
  };
  const contentTypeService = {
    getList: vi.fn(() =>
      of({ items: [{ id: 'ct-1', name: 'Article' }, { id: 'ct-2', name: 'Page' }] }),
    ),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        { provide: SiteSchemaAdminService, useValue: schemaService },
        { provide: FieldAdminService, useValue: fieldService },
        { provide: ContentTypeAdminService, useValue: contentTypeService },
      ],
    });
  });

  it('fetches the schema and caches it across repeated calls', async () => {
    const service = TestBed.inject(SiteReferenceDataService);

    const first = await new Promise(resolve => service.getSchema().subscribe(resolve));
    const second = await new Promise(resolve => service.getSchema().subscribe(resolve));

    expect(first).toEqual({ enabledLanguages: ['en'] });
    expect(second).toEqual({ enabledLanguages: ['en'] });
    expect(schemaService.get).toHaveBeenCalledTimes(1);
  });

  it('refetches the schema after invalidateSchema', async () => {
    const service = TestBed.inject(SiteReferenceDataService);

    await new Promise(resolve => service.getSchema().subscribe(resolve));
    service.invalidateSchema();
    await new Promise(resolve => service.getSchema().subscribe(resolve));

    expect(schemaService.get).toHaveBeenCalledTimes(2);
  });

  it('indexes fields by id and caches across repeated calls', async () => {
    const service = TestBed.inject(SiteReferenceDataService);

    const fields = await new Promise<Map<string, any>>(resolve => service.getFieldsById().subscribe(resolve));
    await new Promise(resolve => service.getFieldsById().subscribe(resolve));

    expect(fields.get('field-1')?.name).toBe('title');
    expect(fields.get('field-2')?.name).toBe('seo');
    expect(fields.size).toBe(2);
    expect(fieldService.getList).toHaveBeenCalledTimes(1);
  });

  it('refetches fields after invalidateFields', async () => {
    const service = TestBed.inject(SiteReferenceDataService);

    await new Promise(resolve => service.getFieldsById().subscribe(resolve));
    service.invalidateFields();
    await new Promise(resolve => service.getFieldsById().subscribe(resolve));

    expect(fieldService.getList).toHaveBeenCalledTimes(2);
  });

  it('indexes field types by name, defaulting a missing indexable flag to true but keeping an explicit false', async () => {
    const service = TestBed.inject(SiteReferenceDataService);

    const byType = await new Promise<Map<string, boolean>>(resolve =>
      service.getIndexableByFieldType().subscribe(resolve),
    );

    expect(byType.get('TextEdit')).toBe(true);
    expect(byType.get('Seo')).toBe(false);
    expect(byType.get('NoAnswer')).toBe(true);
  });

  it('indexes content types by id and caches across repeated calls', async () => {
    const service = TestBed.inject(SiteReferenceDataService);

    const byId = await new Promise<Map<string, any>>(resolve =>
      service.getContentTypesById().subscribe(resolve),
    );
    await new Promise(resolve => service.getContentTypesById().subscribe(resolve));

    expect(byId.get('ct-1')?.name).toBe('Article');
    expect(byId.get('ct-2')?.name).toBe('Page');
    expect(contentTypeService.getList).toHaveBeenCalledTimes(1);
  });

  it('refetches content types after invalidateContentTypes', async () => {
    const service = TestBed.inject(SiteReferenceDataService);

    await new Promise(resolve => service.getContentTypesById().subscribe(resolve));
    service.invalidateContentTypes();
    await new Promise(resolve => service.getContentTypesById().subscribe(resolve));

    expect(contentTypeService.getList).toHaveBeenCalledTimes(2);
  });

  it('keeps each cache independent - invalidating one does not touch the others', async () => {
    const service = TestBed.inject(SiteReferenceDataService);

    await new Promise(resolve => service.getSchema().subscribe(resolve));
    await new Promise(resolve => service.getFieldsById().subscribe(resolve));
    service.invalidateSchema();
    await new Promise(resolve => service.getFieldsById().subscribe(resolve));

    expect(fieldService.getList).toHaveBeenCalledTimes(1);
  });
});
