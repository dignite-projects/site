import { CoreModule, LocalizationService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import { Component, DestroyRef, HostListener, inject, signal, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FlexFieldControlComponent } from '@dignite/ng.flex-fields';
import type { FlexFieldValue } from '@dignite/ng.flex-fields';
import { CKEDITOR_UPLOAD_PROVIDER } from '@dignite/ng.flex-fields-ckeditor';
import { catchError, debounceTime, distinctUntilChanged, forkJoin, map, Observable, of, startWith, switchMap } from 'rxjs';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import type { GetContentPathInput } from '../../proxy/dignite/site/admin/contents/models';
import { ContentTypeAdminService } from '../../proxy/dignite/site/admin/content-types/content-type-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import type { ContentTypeDto } from '../../proxy/dignite/site/content-types/models';
import type { ContentDto } from '../../proxy/dignite/site/contents/models';
import { ContentStatus } from '../../proxy/dignite/site/contents/content-status.enum';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { SiteReferenceDataService } from '../../services/site-reference-data.service';
import { flexFieldErrorMessage } from '../../field-types/flex-field-error-message';
import { SiteCKEditorUploadProvider } from '../../files/services/site-ckeditor-upload-provider';
import { SiteUploadCleanup } from '../../files/services/site-upload-cleanup';
import { arrangeFields, type ArrangedField } from './arranged-field';

const MAX_SLUG_LENGTH = 256;

/** Mirrors `SlugNormalizer.AllowedCharactersPattern` (`src/Dignite.Site.Domain.Shared/SlugNormalizer.cs`). */
const SLUG_PATTERN = /^[\p{L}\p{Nd}_-]*$/u;

/** What the page's route (总体设计 §3.3) says about a content's slug - see {@link ContentEditorComponent.slugState}. */
type SlugState = 'forbidden' | 'required' | 'optional';

/**
 * Creating and editing one content, as a page of its own rather than a dialog.
 *
 * The two halves answer different questions and are sized accordingly: the left is the content itself -
 * however many fields its content type defines, which can be a long form - and the right is the handful
 * of system fields that place and schedule it. A dialog forced both into one scrolling column.
 *
 * Switching the content type rebuilds the left half, because the fields *are* the content type's
 * definition. Values already typed under fields the new type does not have are dropped, which is the
 * honest outcome: they have nowhere to be stored.
 *
 * **Deliberately no CKEditor theme bridge here.** This component used to carry a global
 * `::ng-deep :root` block remapping CKEditor 5's `--ck-color-base-*` tokens onto the host theme -
 * CKEditor ships one fixed light palette with no dark-mode switch, so the editor stayed light in a
 * dark-themed host. `@dignite/ng.flex-fields-ckeditor` >= 10.0.0-rc.13 ships that bridge itself, with
 * a longer fallback chain than this copy had (LeptonX token -> the Bootstrap 5.3 token every ABP
 * Angular theme defines -> CKEditor's own stock literal) plus fixes this copy never had. Re-adding a
 * copy here would not add anything and would fight it at equal `:root` specificity, decided by style
 * injection order alone.
 */
@Component({
  selector: 'site-content-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './content-editor.component.html',
  imports: [CoreModule, ThemeSharedModule, ReactiveFormsModule, RouterLink, FlexFieldControlComponent],
  // The file inputs of the content's fields report their uploads to it, and so do the images of its CKEditor fields: a
  // file uploaded and removed again before the save is deleted once the content is saved, and what an edit uploaded
  // without ever saving is deleted when the editor is left. The CKEditor fields take their upload provider from here,
  // not from the root's, so that the provider they use is created beside this cleanup and finds it.
  providers: [
    SiteUploadCleanup,
    SiteCKEditorUploadProvider,
    { provide: CKEDITOR_UPLOAD_PROVIDER, useExisting: SiteCKEditorUploadProvider },
  ],
})
export class ContentEditorComponent implements OnDestroy {
  private readonly contentService = inject(ContentAdminService);
  private readonly contentTypeService = inject(ContentTypeAdminService);
  private readonly pageService = inject(PageAdminService);
  private readonly referenceData = inject(SiteReferenceDataService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly toaster = inject(ToasterService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly localization = inject(LocalizationService);
  private readonly uploadCleanup = inject(SiteUploadCleanup);
  private readonly destroyRef = inject(DestroyRef);

  readonly ContentStatus = ContentStatus;
  readonly statuses = [ContentStatus.Draft, ContentStatus.Published, ContentStatus.Archived];

  readonly contentId: string | null = this.route.snapshot.paramMap.get('id');
  readonly isEdit = !!this.contentId;

  // State that changes in an HTTP callback is a signal, not a plain field: the host layout's ancestors
  // (`abp-router-outlet`, the LeptonX layouts) are OnPush, and a plain-field write in a response
  // callback never marks them for check, so the form would stay blank (or go stale) until some
  // unrelated event happened to.
  readonly page = signal<PageDto | undefined>(undefined);
  readonly contentTypes = signal<ContentTypeDto[]>([]);
  readonly languages = signal<string[]>([]);
  content?: ContentDto;

  /**
   * Whether the page's own address (an empty slug) is already taken by another content, for the
   * currently selected language - only meaningful, and only checked, when {@link slugState} is
   * `'optional'`. Read by the template to decide whether the "use the page's own address" checkbox is
   * offered at all.
   */
  readonly emptySlotTaken = signal(false);

  private fieldsById = new Map<string, FieldDto>();
  private arrangementByContentType = new Map<string, ArrangedField[]>();

  /**
   * The current content type's fields, as stable `FlexFieldValue`s. Rebuilt only when the content type
   * changes - `ff-flex-field-control` compares this input by object reference, and handing it a freshly
   * allocated array on every change-detection pass would make it tear down and recreate its dynamic
   * child forever (`NG0103`).
   */
  readonly fields = signal<FlexFieldValue[]>([]);

  form?: FormGroup;
  readonly isBusy = signal(false);
  readonly isLoading = signal(true);

  /** Set once the content is saved: the editor is then left for the list, and nothing is unsaved any more. */
  private hasSaved = false;

  constructor() {
    const pageId = this.route.snapshot.queryParamMap.get('pageId');

    forkJoin({
      schema: this.referenceData.getSchema(),
      fieldsById: this.referenceData.getFieldsById(),
      content: this.contentId ? this.contentService.get(this.contentId) : of(undefined),
    })
      .pipe(
        switchMap(loaded => {
          const languages = loaded.schema.enabledLanguages ?? [];
          this.languages.set(languages);
          this.fieldsById = loaded.fieldsById;
          this.content = loaded.content;

          const resolvedPageId = loaded.content?.pageId ?? pageId ?? '';

          return forkJoin({
            page: resolvedPageId ? this.pageService.get(resolvedPageId) : of(undefined),
            contentTypes: resolvedPageId
              ? this.contentTypeService.getListByPage(resolvedPageId)
              : of({ items: [] as ContentTypeDto[] }),
          });
        }),
      )
      .subscribe(({ page, contentTypes }) => {
        this.page.set(page);
        this.contentTypes.set(contentTypes.items ?? []);
        this.rebuildArrangements();
        this.form = this.buildForm();
        this.rebuildFields();
        this.isLoading.set(false);
        this.refreshEmptySlotAvailability();
      });
  }

  get hasMultipleContentTypes(): boolean {
    return this.contentTypes().length > 1;
  }

  /**
   * What this page's route (总体设计 §3.3) says about the slug beneath it, as the server reads it (`PageDto.hasSlug`,
   * `isSlugOptional`). `'forbidden'`: no `{slug}`/`{slug?}` at all, so this page has nothing beneath it but its own
   * address. `'required'`: every content needs one. `'optional'`: an empty slug is also allowed, subject to
   * {@link emptySlotTaken}.
   */
  get slugState(): SlugState {
    const page = this.page();
    return !page?.hasSlug ? 'forbidden' : page.isSlugOptional ? 'optional' : 'required';
  }

  get usePageOwnAddress(): boolean {
    return !!this.form?.get('usePageOwnAddress')?.value;
  }

  /**
   * The address this content will be served at, as the server builds it (`IContentAdminAppService.GetPathAsync`) from
   * the form as it is: a placeholder of the route that nothing is entered for yet shows as its token. Asked again a
   * moment after each change; empty while the form holds what the server refuses, such as a slug with a character a
   * slug cannot have.
   */
  readonly slugPreview = signal('');

  get isDraft(): boolean {
    return this.form?.getRawValue().status === ContentStatus.Draft;
  }

  statusKeyOf(status: ContentStatus): string {
    return `Site::Enum:ContentStatus:${ContentStatus[status]}`;
  }

  fieldErrorMessage(fieldValue: FlexFieldValue): string | null {
    return flexFieldErrorMessage(this.form?.get('flexFields')?.get(fieldValue.field.name), this.localization);
  }

  private rebuildArrangements(): void {
    this.arrangementByContentType = new Map(
      this.contentTypes().map(contentType => [
        contentType.id!,
        arrangeFields(contentType, this.fieldsById),
      ]),
    );
  }

  /** Called on load and whenever the content type changes. See {@link fields}. */
  private rebuildFields(): void {
    const contentTypeId = this.form?.get('contentTypeId')?.value as string | undefined;
    const arranged = this.arrangementByContentType.get(contentTypeId ?? '') ?? [];
    const values = this.content?.fieldValues ?? {};

    this.fields.set(
      arranged.map(field => ({
        field: field.fieldData,
        required: field.required,
        searchable: field.searchable,
        value: values[field.name],
      })),
    );
  }

  onContentTypeChange(): void {
    // The value bag is keyed by field name, so controls for the old type's fields have to go before the
    // new type's are registered - otherwise the payload carries both.
    this.form?.setControl('flexFields', this.fb.group({}));
    this.rebuildFields();
  }

  onStatusChange(): void {
    this.applyPublishTimeAvailability();
  }

  onCultureNameChange(): void {
    this.refreshEmptySlotAvailability();
  }

  onUsePageOwnAddressChange(): void {
    this.applySlugAvailability();
  }

  save(): void {
    if (!this.form || this.form.invalid || this.isBusy()) {
      return;
    }

    this.isBusy.set(true);
    const value = this.form.getRawValue();
    const fieldValues = (this.form.get('flexFields')?.getRawValue() ?? {}) as Record<string, object>;
    const slug = value.slug ?? '';

    const request$ = this.content
      ? this.contentService.update(this.content.id!, {
          contentTypeId: value.contentTypeId,
          slug,
          publishTime: value.publishTime,
          status: value.status,
          fieldValues,
        })
      : this.contentService.create({
          contentTypeId: value.contentTypeId,
          cultureName: value.cultureName,
          slug,
          publishTime: value.publishTime,
          status: value.status,
          fieldValues,
        });

    request$.subscribe({
      next: () => {
        this.isBusy.set(false);
        this.hasSaved = true;
        this.toaster.success('AbpUi::SavedSuccessfully');
        this.uploadCleanup.saved(fieldValues).subscribe();
        this.backToList();
      },
      error: () => this.isBusy.set(false),
    });
  }

  backToList(): void {
    this.router.navigate(['/site/contents']);
  }

  /**
   * Whether the editor may be left now, for the route's `canDeactivate`: at once when there is nothing to lose, and
   * otherwise when the user confirms in a dialog that has to be answered. What is lost is the form's changes and, when
   * the edit uploaded any, those files, which `ngOnDestroy` deletes.
   */
  canLeave(): boolean | Observable<boolean> {
    if (!this.hasUnsavedWork()) {
      return true;
    }

    return this.confirmation
      .warn(
        this.uploadCleanup.hasUploads() ? 'Site::LeaveEditorWithUploadsConfirm' : 'Site::LeaveEditorConfirm',
        'AbpUi::AreYouSure',
      )
      .pipe(map(status => status === Confirmation.Status.confirm));
  }

  /** The browser's own confirmation when the tab is closed or the page reloaded; it cannot be worded, and nothing is cleaned up. */
  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedWork()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  /**
   * Whether leaving would lose something: the form was changed, or the edit uploaded a file. Not while the content is
   * being saved, which goes on without the editor, nor once it was saved.
   */
  private hasUnsavedWork(): boolean {
    return !this.hasSaved && !this.isBusy() && (!!this.form?.dirty || this.uploadCleanup.hasUploads());
  }

  ngOnDestroy(): void {
    // A save that is still running calls `saved` when it answers, and that decides which uploads the content holds; one
    // that already answered left nothing to discard.
    if (!this.isBusy()) {
      this.uploadCleanup.discard().subscribe();
    }
  }

  private buildForm(): FormGroup {
    const content = this.content;

    const form = this.fb.group({
      contentTypeId: [
        content?.contentTypeId ?? this.contentTypes()[0]?.id ?? '',
        Validators.required,
      ],
      cultureName: [
        {
          value:
            content?.cultureName ??
            this.route.snapshot.queryParamMap.get('cultureName') ??
            this.languages()[0] ??
            '',
          // Fixed after creation: the language is half the natural key that groups a content's
          // translations, so changing it would silently move the content to another translation set.
          disabled: !!content,
        },
        Validators.required,
      ],
      // Editing a content that is already the page's own (an empty slug) starts the checkbox checked -
      // otherwise applySlugAvailability would immediately overwrite its empty slug with a forced-required
      // value the moment the form builds.
      usePageOwnAddress: [!!content && !content.slug],
      slug: [content?.slug ?? '', [Validators.maxLength(MAX_SLUG_LENGTH), Validators.pattern(SLUG_PATTERN)]],
      publishTime: [this.toLocalDateTime(content?.publishTime), Validators.required],
      status: [content?.status ?? ContentStatus.Published],
      flexFields: this.fb.group({}),
    });

    this.form = form;
    this.applySlugAvailability();
    this.applyPublishTimeAvailability();
    this.watchPath(form);

    return form;
  }

  /**
   * Enables/disables and (in)validates the `slug` control from {@link slugState} - and, in the
   * `'optional'` case, from whether the "use the page's own address" checkbox is ticked. The same
   * three-way split `ContentManager.CheckSlugRequirementAsync` enforces server-side; this only decides
   * what the form looks like before that ever runs.
   */
  private applySlugAvailability(): void {
    const slug = this.form?.get('slug');
    if (!slug) {
      return;
    }

    const forceOwnAddress = this.slugState === 'forbidden' ||
      (this.slugState === 'optional' && this.usePageOwnAddress);

    if (forceOwnAddress) {
      slug.setValue('');
      slug.clearValidators();
      slug.disable();
    } else {
      slug.enable();
      slug.setValidators([
        Validators.required,
        Validators.maxLength(MAX_SLUG_LENGTH),
        Validators.pattern(SLUG_PATTERN),
      ]);
    }

    slug.updateValueAndValidity();
  }

  /**
   * Looks up whether this page's own address (an empty slug) is already taken for the selected
   * language, so the "use the page's own address" option can be withheld before the editor ever submits
   * and hits `Site:040001`. A no-op outside the `'optional'` state, where the question does not arise.
   *
   * Goes through `getList` rather than the more direct `findBySlug`, which exists for exactly this
   * lookup - `IContentAdminAppService.FindBySlugAsync(pageId, cultureName, "")` is the same query
   * `SiteRouteResolver` uses to resolve a bare page match. But ABP's `RestService.getParams` silently
   * drops any query parameter whose value is an empty string, so a `slug` of `""` never reaches the
   * request at all; the server then sees a *missing* parameter, not an intentionally empty one, and
   * rejects it exactly as `Should_Reject_A_Null_Slug` says it must. Listing this page's contents for the
   * language and filtering for an empty slug client-side sidesteps that without weakening the server's
   * null-vs-empty distinction.
   */
  private refreshEmptySlotAvailability(): void {
    const pageId = this.page()?.id;
    if (this.slugState !== 'optional' || !pageId) {
      this.emptySlotTaken.set(false);
      return;
    }

    const cultureName = this.form?.get('cultureName')?.value;
    if (!cultureName) {
      return;
    }

    this.contentService
      .getList({ pageId, cultureName, maxResultCount: 1000 })
      .pipe(catchError(() => of({ items: [] as ContentDto[], totalCount: 0 })))
      .subscribe(result => {
        const existing = (result.items ?? []).find(item => item.slug === '');
        const taken = !!existing && existing.id !== this.content?.id;
        this.emptySlotTaken.set(taken);

        // The slot this checkbox promised is gone (e.g. the language was switched to one where it is
        // already taken) - un-tick it rather than leave a checked box the next save would reject.
        if (taken && this.usePageOwnAddress) {
          this.form?.get('usePageOwnAddress')?.setValue(false);
          this.applySlugAvailability();
        }
      });
  }

  /**
   * A draft is never public, so a publish time on one is not a schedule anything acts on - the server
   * rejects the combination outright (`Site:040003`). Scheduling is Published plus a future time.
   */
  private applyPublishTimeAvailability(): void {
    const publishTime = this.form?.get('publishTime');
    const status = this.form?.get('status')?.value as ContentStatus | undefined;

    if (!publishTime) {
      return;
    }

    if (status === ContentStatus.Draft) {
      publishTime.setValue(this.toLocalDateTime());
      publishTime.disable();
    } else {
      publishTime.enable();
    }
  }

  /** `<input type="datetime-local">` wants `yyyy-MM-ddTHH:mm`, not an ISO string with a zone. */
  private toLocalDateTime(value?: string): string {
    const date = value ? new Date(value) : new Date();
    const offset = date.getTimezoneOffset() * 60_000;
    return new Date(date.getTime() - offset).toISOString().slice(0, 16);
  }

  /** Keeps {@link slugPreview} up to date with the form. */
  private watchPath(form: FormGroup): void {
    form.valueChanges
      .pipe(
        startWith(null),
        debounceTime(300),
        map(() => this.pathInput(form)),
        distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
        switchMap(input =>
          input
            ? this.contentService.getPath(input, { skipHandleError: true }).pipe(catchError(() => of('')))
            : of(''),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(path => this.slugPreview.set(path));
  }

  private pathInput(form: FormGroup): GetContentPathInput | null {
    const pageId = this.page()?.id;
    const value = form.getRawValue();
    if (!pageId || !value.cultureName) {
      return null;
    }

    return {
      pageId,
      cultureName: value.cultureName,
      slug: value.slug ?? '',
      publishTime: value.publishTime,
      fieldValues: form.get('flexFields')?.getRawValue() ?? {},
    };
  }
}
