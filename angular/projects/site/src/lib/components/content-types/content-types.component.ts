import { CoreModule, PermissionDirective } from '@abp/ng.core';
import {
  Confirmation,
  ConfirmationService,
  ThemeSharedModule,
  ToasterService,
} from '@abp/ng.theme.shared';
import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ContentTypeAdminService } from '../../proxy/dignite/site/admin/content-types/content-type-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import type {
  ContentTypeDto,
  ContentTypeFieldDto,
} from '../../proxy/dignite/site/content-types/models';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { eSitePolicyNames } from '../../enums/policy-names';
import { SiteReferenceDataService } from '../../services/site-reference-data.service';
import { FieldArrangementComponent } from './field-arrangement.component';

const MAX_NAME_LENGTH = 64;
const MAX_DISPLAY_NAME_LENGTH = 128;
const MAX_DESCRIPTION_LENGTH = 512;

/** The content types of one page. Reached from the Pages list; there is no menu entry. */
@Component({
  selector: 'site-content-types',
  templateUrl: './content-types.component.html',
  imports: [
    CoreModule,
    ThemeSharedModule,
    ReactiveFormsModule,
    PermissionDirective,
    FieldArrangementComponent,
  ],
})
export class ContentTypesComponent {
  private readonly contentTypeService = inject(ContentTypeAdminService);
  private readonly pageService = inject(PageAdminService);
  private readonly referenceData = inject(SiteReferenceDataService);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);

  readonly policies = eSitePolicyNames;

  readonly pageId: string = this.route.snapshot.paramMap.get('pageId') ?? '';

  page?: PageDto;
  contentTypes: ContentTypeDto[] = [];
  fieldsById = new Map<string, FieldDto>();
  indexableByFieldType = new Map<string, boolean>();

  isModalOpen = false;
  isModalBusy = false;
  editingContentType?: ContentTypeDto;
  form?: FormGroup;

  /**
   * The complete arrangement being edited. Held here, not inside the form, because it is saved whole:
   * `UpdateContentTypeDto.Fields` replaces the server's list outright, so anything missing from this
   * array comes back reset to its default.
   */
  arrangement: ContentTypeFieldDto[] = [];

  @ViewChild('submitButton') submitButton?: ElementRef<HTMLButtonElement>;

  constructor() {
    this.pageService.get(this.pageId).subscribe(page => (this.page = page));

    forkJoin({
      fields: this.referenceData.getFieldsById(),
      indexable: this.referenceData.getIndexableByFieldType(),
    }).subscribe(({ fields, indexable }) => {
      this.fieldsById = fields;
      this.indexableByFieldType = indexable;
    });

    this.load();
  }

  load(): void {
    this.contentTypeService
      .getListByPage(this.pageId)
      .subscribe(result => (this.contentTypes = result.items ?? []));
  }

  fieldCountOf(contentType: ContentTypeDto): number {
    return contentType.fields?.length ?? 0;
  }

  openCreate(): void {
    this.editingContentType = undefined;
    this.arrangement = [];
    this.form = this.buildForm();
    this.isModalOpen = true;
  }

  openEdit(contentType: ContentTypeDto): void {
    this.editingContentType = contentType;
    // A copy: abandoning the modal must not leave the list's row mutated by the arrangement editor.
    this.arrangement = (contentType.fields ?? [])
      .map(entry => ({ ...entry }))
      .sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
    this.form = this.buildForm(contentType);
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.form = undefined;
    this.editingContentType = undefined;
    this.arrangement = [];
  }

  save(): void {
    if (!this.form || this.form.invalid || this.isModalBusy) {
      return;
    }

    this.isModalBusy = true;
    const value = this.form.getRawValue();

    const request$ = this.editingContentType
      ? this.contentTypeService.update(this.editingContentType.id!, {
          name: value.name,
          displayName: value.displayName,
          description: value.description,
          fields: this.arrangement,
        })
      : this.contentTypeService.create({
          pageId: this.pageId,
          name: value.name,
          displayName: value.displayName,
          description: value.description,
          fields: this.arrangement,
        });

    request$.subscribe({
      next: () => {
        this.isModalBusy = false;
        this.closeModal();
        this.load();
        this.referenceData.invalidateContentTypes();
        this.toaster.success('AbpUi::SavedSuccessfully');
      },
      error: () => (this.isModalBusy = false),
    });
  }

  delete(contentType: ContentTypeDto): void {
    this.confirmation
      .warn('AbpUi::ItemWillBeDeletedMessage', 'AbpUi::AreYouSure', {
        messageLocalizationParams: [contentType.displayName ?? ''],
      })
      .subscribe((status: Confirmation.Status) => {
        if (status === Confirmation.Status.confirm) {
          // Answers ContentTypeStillHasContents when contents remain; the server's message says so.
          this.contentTypeService.delete(contentType.id!).subscribe(() => {
            this.load();
            this.referenceData.invalidateContentTypes();
            this.toaster.success('AbpUi::SuccessfullyDeleted');
          });
        }
      });
  }

  private buildForm(contentType?: ContentTypeDto): FormGroup {
    return this.fb.group({
      displayName: [
        contentType?.displayName ?? '',
        [Validators.required, Validators.maxLength(MAX_DISPLAY_NAME_LENGTH)],
      ],
      name: [
        contentType?.name ?? '',
        [Validators.required, Validators.maxLength(MAX_NAME_LENGTH)],
      ],
      description: [
        contentType?.description ?? '',
        [Validators.maxLength(MAX_DESCRIPTION_LENGTH)],
      ],
    });
  }
}
