import { CoreModule, PermissionDirective, PermissionService } from '@abp/ng.core';
import {
  Confirmation,
  ConfirmationService,
  ThemeSharedModule,
  ToasterService,
} from '@abp/ng.theme.shared';
import {
  Component,
  ElementRef,
  ViewChild,
  inject,
  signal,
  ChangeDetectionStrategy,
} from '@angular/core';
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

/** Mirrors `IdentifierName.Pattern` (`src/Dignite.Site.Domain.Shared/IdentifierName.cs`). */
const NAME_PATTERN = /^[a-z0-9][a-z0-9_-]*$/;

/** The content types of one page. Reached from the Pages list; there is no menu entry. */
@Component({
  selector: 'site-content-types',
  changeDetection: ChangeDetectionStrategy.OnPush,
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
  private readonly permission = inject(PermissionService);

  readonly policies = eSitePolicyNames;

  readonly pageId: string = this.route.snapshot.paramMap.get('pageId') ?? '';

  // State that changes in an HTTP callback is a signal, not a plain field: the host layout's ancestors
  // (`abp-router-outlet`, the LeptonX layouts) are OnPush, and a plain-field write in a response
  // callback never marks them for check, so the page would stay empty until some unrelated event did.
  readonly page = signal<PageDto | undefined>(undefined);
  readonly contentTypes = signal<ContentTypeDto[]>([]);
  readonly fieldsById = signal(new Map<string, FieldDto>());
  readonly fieldsLoaded = signal(false);
  readonly indexableByFieldType = signal(new Map<string, boolean>());

  readonly isModalOpen = signal(false);
  readonly isModalBusy = signal(false);
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
    this.pageService.get(this.pageId).subscribe(page => this.page.set(page));

    forkJoin({
      fields: this.referenceData.getFieldsById(),
      indexable: this.referenceData.getIndexableByFieldType(),
    }).subscribe(({ fields, indexable }) => {
      this.fieldsById.set(fields);
      this.fieldsLoaded.set(true);
      this.indexableByFieldType.set(indexable);
    });

    this.load(true);
  }

  /**
   * `autoOpenIfSingle` only applies to this initial load: a page with exactly one content type has
   * nothing to disambiguate, so arriving here always means "go edit it". Reloads after a save or delete
   * must not repeat this - otherwise closing the edit modal after a successful save would immediately
   * reopen it.
   */
  load(autoOpenIfSingle = false): void {
    this.contentTypeService.getListByPage(this.pageId).subscribe(result => {
      const contentTypes = result.items ?? [];
      this.contentTypes.set(contentTypes);

      if (
        autoOpenIfSingle &&
        contentTypes.length === 1 &&
        this.permission.getGrantedPolicy(this.policies.ContentTypesUpdate)
      ) {
        this.openEdit(contentTypes[0]);
      }
    });
  }

  fieldCountOf(contentType: ContentTypeDto): number {
    return contentType.fields?.length ?? 0;
  }

  openCreate(): void {
    this.editingContentType = undefined;
    this.arrangement = [];
    this.form = this.buildForm();
    this.isModalOpen.set(true);
  }

  openEdit(contentType: ContentTypeDto): void {
    this.editingContentType = contentType;
    // A copy: abandoning the modal must not leave the list's row mutated by the arrangement editor.
    this.arrangement = (contentType.fields ?? [])
      .map(entry => ({ ...entry }))
      .sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
    this.form = this.buildForm(contentType);
    this.isModalOpen.set(true);
  }

  closeModal(): void {
    this.isModalOpen.set(false);
    this.form = undefined;
    this.editingContentType = undefined;
    this.arrangement = [];
  }

  save(): void {
    if (!this.form || this.form.invalid || this.isModalBusy()) {
      return;
    }

    this.isModalBusy.set(true);
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
        this.isModalBusy.set(false);
        this.closeModal();
        this.load();
        this.referenceData.invalidateContentTypes();
        this.toaster.success('AbpUi::SavedSuccessfully');
      },
      error: () => this.isModalBusy.set(false),
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
        [Validators.required, Validators.maxLength(MAX_NAME_LENGTH), Validators.pattern(NAME_PATTERN)],
      ],
      description: [
        contentType?.description ?? '',
        [Validators.maxLength(MAX_DESCRIPTION_LENGTH)],
      ],
    });
  }
}
