import { Injectable, inject } from '@angular/core';
import { Observable, map, shareReplay } from 'rxjs';
import { ContentTypeAdminService } from '../proxy/dignite/site/admin/content-types/content-type-admin.service';
import { FieldAdminService } from '../proxy/dignite/site/admin/fields/field-admin.service';
import { SiteSchemaAdminService } from '../proxy/dignite/site/admin/schema/site-schema-admin.service';
import type { SiteSchemaDto } from '../proxy/dignite/site/admin/schema/models';
import type { ContentTypeDto } from '../proxy/dignite/site/content-types/models';
import type { FieldDto } from '../proxy/dignite/site/fields/models';

/**
 * The reference data every screen needs but none of them owns: the site's enabled languages, the whole
 * field library, and which field types can be indexed. Each is fetched once and replayed; call the
 * matching `invalidate*` after a write that would change it.
 *
 * **On the schema endpoint.** `GET /api/site-admin/schema` is keyed by *name* all the way down -
 * `SiteSchemaContentTypeDto` and `SiteSchemaFieldDto` carry no id. That shape exists for MCP, which
 * addresses everything by name. The admin UI cannot resolve a content type's fields through it, because
 * a `ContentDto` identifies its type only by `contentTypeId` (a Guid) and there is nothing to join a
 * Guid to a name on. So the schema is used here for site-level settings only; field resolution goes
 * through {@link getFieldsById}, joined against `ContentTypeDto.fields[].fieldId`.
 */
@Injectable({ providedIn: 'root' })
export class SiteReferenceDataService {
  private readonly schemaService = inject(SiteSchemaAdminService);
  private readonly fieldService = inject(FieldAdminService);
  private readonly contentTypeService = inject(ContentTypeAdminService);

  private schemaCache?: Observable<SiteSchemaDto>;
  private fieldsCache?: Observable<Map<string, FieldDto>>;
  private indexableCache?: Observable<Map<string, boolean>>;
  private compositeCache?: Observable<ReadonlySet<string>>;
  private contentTypesCache?: Observable<Map<string, ContentTypeDto>>;

  /** Site-level settings. `enabledLanguages[0]` is the default language. */
  getSchema(): Observable<SiteSchemaDto> {
    this.schemaCache ??= this.schemaService
      .get()
      .pipe(shareReplay({ bufferSize: 1, refCount: false }));
    return this.schemaCache;
  }

  /** The whole field library keyed by id - `ContentTypeFieldDto` carries only `fieldId`. */
  getFieldsById(): Observable<Map<string, FieldDto>> {
    this.fieldsCache ??= this.fieldService
      .getList({})
      .pipe(
        map(result => new Map((result.items ?? []).map(field => [field.id!, field]))),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.fieldsCache;
  }

  /**
   * Whether a field type has a query-index slot at all, keyed by field type name. Straight from the
   * server: the answer is `IFieldType.IndexValueType`, and the Angular flex-fields library deliberately
   * does not restate it. A type that answers `false` can never be searched however its `searchable` flag
   * is set - `FlexFieldIndexManagerBase.GetIndexableFieldsAsync` skips it regardless.
   */
  getIndexableByFieldType(): Observable<Map<string, boolean>> {
    this.indexableCache ??= this.fieldService
      .getFieldTypes()
      .pipe(
        map(result => new Map((result.items ?? []).map(type => [type.name ?? '', type.indexable ?? true]))),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.indexableCache;
  }

  /**
   * Which field types declare further fields inside their own configuration - `Matrix` and `Table`.
   * Straight from the server for the same reason {@link getIndexableByFieldType} is: the answer is
   * `ICompositeFieldType`, and a list restated here would be one more thing to remember when a third
   * composite type is added.
   *
   * Used by the composite config editors to stop offering composite types once
   * `MAX_COMPOSITE_NESTING_DEPTH` is reached - see `composite-nesting.ts`.
   */
  getCompositeFieldTypeNames(): Observable<ReadonlySet<string>> {
    this.compositeCache ??= this.fieldService
      .getFieldTypes()
      .pipe(
        map(
          result =>
            new Set((result.items ?? []).filter(type => type.composite).map(type => type.name ?? '')),
        ),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.compositeCache;
  }

  /**
   * Every content type, across every page, keyed by id - `ContentDto` carries only `contentTypeId`, and
   * `ContentTypeAdminService.getListByPage` alone cannot resolve a name for a row whose page is not the
   * one currently selected in a filter.
   */
  getContentTypesById(): Observable<Map<string, ContentTypeDto>> {
    this.contentTypesCache ??= this.contentTypeService
      .getList()
      .pipe(
        map(result => new Map((result.items ?? []).map(type => [type.id!, type]))),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.contentTypesCache;
  }

  invalidateFields(): void {
    this.fieldsCache = undefined;
  }

  invalidateSchema(): void {
    this.schemaCache = undefined;
  }

  invalidateContentTypes(): void {
    this.contentTypesCache = undefined;
  }
}
