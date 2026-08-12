import { CoreModule } from '@abp/ng.core';
import { ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FlexFieldControlComponent } from '@dignite/ng.flex-fields';
import type { FlexFieldData, FlexFieldValue } from '@dignite/ng.flex-fields';
import { catchError, forkJoin, of, switchMap } from 'rxjs';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import { ContentTypeAdminService } from '../../proxy/dignite/site/admin/content-types/content-type-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import type { ContentTypeDto } from '../../proxy/dignite/site/content-types/models';
import type { ContentDto } from '../../proxy/dignite/site/contents/models';
import { ContentStatus } from '../../proxy/dignite/site/contents/content-status.enum';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { SiteReferenceDataService } from '../../services/site-reference-data.service';
import type { ArrangedField } from './arranged-field';

const MAX_SLUG_LENGTH = 256;
const OPTIONAL_SLUG_TOKEN = '{slug?}';
const REQUIRED_SLUG_TOKEN = '{slug}';

/** Mirrors `SlugNormalizer.AllowedCharactersPattern` (`src/Dignite.Site.Domain.Shared/SlugNormalizer.cs`). */
const SLUG_PATTERN = /^[\p{L}\p{Nd}_-]*$/u;

/** Mirrors `PageRoute.HasSlug`/`PageRoute.IsSlugOptional` (总体设计 §3.3) - see {@link ContentEditorComponent.slugState}. */
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
 */
@Component({
  selector: 'site-content-editor',
  templateUrl: './content-editor.component.html',
  imports: [CoreModule, ThemeSharedModule, ReactiveFormsModule, RouterLink, FlexFieldControlComponent],
})
export class ContentEditorComponent {
  private readonly contentService = inject(ContentAdminService);
  private readonly contentTypeService = inject(ContentTypeAdminService);
  private readonly pageService = inject(PageAdminService);
  private readonly referenceData = inject(SiteReferenceDataService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly toaster = inject(ToasterService);

  readonly ContentStatus = ContentStatus;
  readonly statuses = [ContentStatus.Draft, ContentStatus.Published, ContentStatus.Archived];

  readonly contentId: string | null = this.route.snapshot.paramMap.get('id');
  readonly isEdit = !!this.contentId;

  page?: PageDto;
  contentTypes: ContentTypeDto[] = [];
  languages: string[] = [];
  content?: ContentDto;

  /**
   * Whether the page's own address (an empty slug) is already taken by another content, for the
   * currently selected language - only meaningful, and only checked, when {@link slugState} is
   * `'optional'`. Read by the template to decide whether the "use the page's own address" checkbox is
   * offered at all.
   */
  emptySlotTaken = false;

  private fieldsById = new Map<string, FieldDto>();
  private arrangementByContentType = new Map<string, ArrangedField[]>();

  /**
   * The current content type's fields, as stable `FlexFieldValue`s. Rebuilt only when the content type
   * changes - `ff-flex-field-control` compares this input by object reference, and handing it a freshly
   * allocated array on every change-detection pass would make it tear down and recreate its dynamic
   * child forever (`NG0103`).
   */
  fields: FlexFieldValue[] = [];

  form?: FormGroup;
  isBusy = false;
  isLoading = true;

  constructor() {
    const pageId = this.route.snapshot.queryParamMap.get('pageId');

    forkJoin({
      schema: this.referenceData.getSchema(),
      fieldsById: this.referenceData.getFieldsById(),
      content: this.contentId ? this.contentService.get(this.contentId) : of(undefined),
    })
      .pipe(
        switchMap(loaded => {
          this.languages = loaded.schema.enabledLanguages ?? [];
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
        this.page = page;
        this.contentTypes = contentTypes.items ?? [];
        this.rebuildArrangements();
        this.form = this.buildForm();
        this.rebuildFields();
        this.isLoading = false;
        this.refreshEmptySlotAvailability();
      });
  }

  get hasMultipleContentTypes(): boolean {
    return this.contentTypes.length > 1;
  }

  /**
   * What this page's route (总体设计 §3.3) says about the slug beneath it - the front-end mirror of
   * `PageRoute.HasSlug`/`PageRoute.IsSlugOptional`. `'forbidden'`: no `{slug}`/`{slug?}` at all, so this
   * page has nothing beneath it but its own address. `'required'`: a bare `{slug}`, so every content
   * needs one. `'optional'`: `{slug?}`, so an empty slug is also allowed, subject to
   * {@link emptySlotTaken}.
   */
  get slugState(): SlugState {
    const route = this.page?.route ?? '';
    if (route.includes(OPTIONAL_SLUG_TOKEN)) {
      return 'optional';
    }
    return route.includes(REQUIRED_SLUG_TOKEN) ? 'required' : 'forbidden';
  }

  get usePageOwnAddress(): boolean {
    return !!this.form?.get('usePageOwnAddress')?.value;
  }

  /**
   * The address this content will actually be served at, recomputed on every read so the template can
   * bind it directly and have it track the slug field as the user types. An approximation, not the
   * source of truth: {@link resolvePlaceholder} resolves every placeholder generically off the form and
   * the loaded content, falling back to the raw token when it cannot - `Page.BuildContentPath` on the
   * server is what actually decides the URL.
   *
   * The empty-slug branch mirrors `Page.BuildContentPath` itself, not a naming convention: an empty
   * slug is short-circuited to the page's own path there regardless of what the rest of the route
   * contains, so the preview has to do the same rather than run it through the placeholder replacement
   * below.
   */
  get slugPreview(): string {
    const route = this.page?.route;
    if (!route) {
      return '';
    }

    const slug = (this.form?.get('slug')?.value ?? '').trim();
    return slug ? this.buildPreviewPath(route) : this.pageOwnPath(route);
  }

  get isDraft(): boolean {
    return this.form?.getRawValue().status === ContentStatus.Draft;
  }

  statusKeyOf(status: ContentStatus): string {
    return `Site::Enum:ContentStatus:${ContentStatus[status]}`;
  }

  private rebuildArrangements(): void {
    this.arrangementByContentType = new Map(
      this.contentTypes.map(contentType => [
        contentType.id!,
        (contentType.fields ?? [])
          .map(entry => {
            const definition = entry.fieldId ? this.fieldsById.get(entry.fieldId) : undefined;
            if (!definition) {
              return undefined;
            }

            const fieldData: FlexFieldData = {
              id: definition.id!,
              name: definition.name!,
              displayName: entry.displayName || definition.displayName!,
              description: definition.description ?? undefined,
              fieldTypeName: definition.fieldTypeName!,
              configuration: (definition.configuration ?? {}) as Record<string, unknown>,
            };

            return {
              fieldId: definition.id!,
              name: definition.name!,
              displayName: fieldData.displayName,
              fieldTypeName: definition.fieldTypeName!,
              required: !!entry.required,
              searchable: !!entry.searchable,
              showInList: !!entry.showInList,
              order: entry.order ?? 0,
              fieldData,
            } satisfies ArrangedField;
          })
          .filter((field): field is ArrangedField => !!field)
          .sort((a, b) => a.order - b.order),
      ]),
    );
  }

  /** Called on load and whenever the content type changes. See {@link fields}. */
  private rebuildFields(): void {
    const contentTypeId = this.form?.get('contentTypeId')?.value as string | undefined;
    const arranged = this.arrangementByContentType.get(contentTypeId ?? '') ?? [];
    const values = this.content?.fieldValues ?? {};

    this.fields = arranged.map(field => ({
      field: field.fieldData,
      required: field.required,
      searchable: field.searchable,
      value: values[field.name],
    }));
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
    if (!this.form || this.form.invalid || this.isBusy) {
      return;
    }

    this.isBusy = true;
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
        this.isBusy = false;
        this.toaster.success('AbpUi::SavedSuccessfully');
        this.backToList();
      },
      error: () => (this.isBusy = false),
    });
  }

  backToList(): void {
    this.router.navigate(['/site/contents']);
  }

  private buildForm(): FormGroup {
    const content = this.content;

    const form = this.fb.group({
      contentTypeId: [
        content?.contentTypeId ?? this.contentTypes[0]?.id ?? '',
        Validators.required,
      ],
      cultureName: [
        {
          value:
            content?.cultureName ??
            this.route.snapshot.queryParamMap.get('cultureName') ??
            this.languages[0] ??
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
      status: [content?.status ?? ContentStatus.Draft],
      flexFields: this.fb.group({}),
    });

    this.form = form;
    this.applySlugAvailability();
    this.applyPublishTimeAvailability();

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
    if (this.slugState !== 'optional' || !this.page?.id) {
      this.emptySlotTaken = false;
      return;
    }

    const cultureName = this.form?.get('cultureName')?.value;
    if (!cultureName) {
      return;
    }

    this.contentService
      .getList({ pageId: this.page.id, cultureName, maxResultCount: 1000 })
      .pipe(catchError(() => of({ items: [] as ContentDto[], totalCount: 0 })))
      .subscribe(result => {
        const existing = (result.items ?? []).find(item => item.slug === '');
        this.emptySlotTaken = !!existing && existing.id !== this.content?.id;

        // The slot this checkbox promised is gone (e.g. the language was switched to one where it is
        // already taken) - un-tick it rather than leave a checked box the next save would reject.
        if (this.emptySlotTaken && this.usePageOwnAddress) {
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

  /** Mirrors `PageRoute.GetPath`: everything up to the segment that first carries a placeholder. */
  private pageOwnPath(route: string): string {
    const index = route.indexOf('{');
    if (index < 0) {
      return route;
    }

    const boundary = route.lastIndexOf('/', index);
    return boundary <= 0 ? '/' : route.slice(0, boundary);
  }

  /**
   * A rough, client-side mirror of `PageRoute.Build` - see {@link slugPreview}'s remarks. Every
   * placeholder, `{slug}` included, goes through the same generic {@link resolvePlaceholder} - no
   * per-name branch here, deliberately: the server does not have one either, it reflects over `Content`
   * (`Page.ResolveFieldValue`), and a name-by-name `if` ladder here would just be that same mistake
   * moved to TypeScript instead of removed.
   */
  private buildPreviewPath(route: string): string {
    const canonical = route.replace(OPTIONAL_SLUG_TOKEN, REQUIRED_SLUG_TOKEN);

    return canonical.replace(/\{([a-zA-Z][a-zA-Z0-9]*)(:([^}]+))?\}/g, (token, name, _colonFormat, format) =>
      this.resolvePlaceholder(name, format ?? null) ?? token,
    );
  }

  /**
   * Resolves one placeholder's preview text purely from *where* `name` is actually found, never from
   * what it is called. Angular forms have no runtime type to reflect over the way `Content` does server
   * side - `ContentDto` is a TypeScript interface, erased at compile time - so the nearest equivalent is
   * a generic, case-insensitive walk of what is actually on the form and the loaded content, checked in
   * the same order the server would find them: a real property first, `FlexFields` second.
   * 1. A top-level form control - every system field the editor can change lives here (`cultureName`,
   *    `status`, `publishTime`, `contentTypeId`, `slug`...), whatever it happens to be named.
   * 2. A control inside `flexFields` - a content type's own business fields.
   * 3. A property already on the loaded `this.content` DTO - covers the read-only audit trail
   *    (`id`, `creationTime`, ...) `FullAuditedEntityDto` carries but no form control exists for.
   *    Naturally empty until this content has actually been saved once - which is honest, since the
   *    server has nothing to reflect there yet either.
   *
   * One deliberate, named exception to "no name-based branching": {@link usePageOwnAddress} is a UI-only
   * control with no server-side counterpart, and step 1 cannot tell that apart from a real field by
   * shape alone - a route that actually referenced `{usePageOwnAddress}` would resolve to "true"/"false"
   * here. Excluding it by name would be exactly the kind of branch this method exists to avoid, and the
   * failure mode is harmless: `Page.BuildContentPath` has no such property either, so a route that made
   * this mistake for real still fails loudly server-side the first time a URL is actually built from it.
   */
  private resolvePlaceholder(name: string, format: string | null): string | null {
    const raw = this.findValue(name);
    if (raw === null || raw === undefined || raw === '') {
      return null;
    }

    if (format) {
      const formatted = this.tryFormatByShape(raw, format);
      if (formatted !== null) {
        return formatted;
      }
    }

    return String(raw);
  }

  /**
   * Applies `format` to `raw` purely from what `raw` looks like, never from which placeholder produced
   * it - the same dispatch-by-shape rule {@link resolvePlaceholder} follows, extended to every shape
   * this preview knows how to format. Mirrors `Page.FormatFieldValue`'s `value is IFormattable` check,
   * just done by sniffing a form value's shape with a regex instead of asking the CLR for its runtime
   * type - there is no such type to ask here, `raw` is already unwrapped to a plain JS value by the time
   * it reaches this method.
   * <p>
   * Each shape is tried in turn; the first one whose regex matches `raw` owns the answer from there,
   * whether or not it actually recognizes `format` - a date-shaped value with a nonsense format is a
   * date that failed to format, not a number or a Guid that happened to also look like one. Recognizing
   * the shape but not the specific `format` asked of it (a Guid's `X`, a numeric format outside
   * {@link formatNumber}'s subset) returns null, same as a shape nothing here recognizes at all - both
   * fall back to the unformatted value in {@link resolvePlaceholder}, deliberately: this is a preview,
   * and a plausible but possibly-wrong guess at an unimplemented format would be worse than showing the
   * plain value.
   */
  private tryFormatByShape(raw: unknown, format: string): string | null {
    const date = this.tryParseDate(raw);
    if (date) {
      return this.formatDate(date, format);
    }

    const guid = this.tryParseGuid(raw);
    if (guid) {
      return this.formatGuid(guid, format);
    }

    const number = this.tryParseNumber(raw);
    if (number !== null) {
      return this.formatNumber(number, format);
    }

    return null;
  }

  private findValue(name: string): unknown {
    const lowerName = name.toLowerCase();

    const topLevelControls = this.form?.controls;
    const topLevelKey = topLevelControls && Object.keys(topLevelControls).find(key => key.toLowerCase() === lowerName);
    if (topLevelKey) {
      return this.form!.get(topLevelKey)!.value;
    }

    const flexFields = this.form?.get('flexFields');
    if (flexFields instanceof FormGroup) {
      const flexKey = Object.keys(flexFields.controls).find(key => key.toLowerCase() === lowerName);
      if (flexKey) {
        return flexFields.get(flexKey)!.value;
      }
    }

    if (this.content) {
      const contentRecord = this.content as unknown as Record<string, unknown>;
      const contentKey = Object.keys(contentRecord).find(key => key.toLowerCase() === lowerName);
      if (contentKey !== undefined) {
        return contentRecord[contentKey];
      }
    }

    return undefined;
  }

  /** Whether `value` is shaped like a `datetime-local` input's own output (`yyyy-MM-ddTHH:mm`). */
  private tryParseDate(value: unknown): Date | null {
    if (typeof value !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(value)) {
      return null;
    }

    const date = new Date(value);
    return isNaN(date.getTime()) ? null : date;
  }

  /** Only the three specifiers a `:FORMAT` suffix may actually contain (总体设计 §3.3) - no '/' among them. */
  private formatDate(date: Date, format: string): string {
    return format
      .replace(/yyyy/g, String(date.getFullYear()))
      .replace(/yy/g, String(date.getFullYear()).slice(-2))
      .replace(/MM/g, String(date.getMonth() + 1).padStart(2, '0'))
      .replace(/dd/g, String(date.getDate()).padStart(2, '0'));
  }

  /** Whether `value` is shaped like the canonical, hyphenated form of a Guid. */
  private tryParseGuid(value: unknown): string | null {
    return typeof value === 'string' &&
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
      ? value
      : null;
  }

  /**
   * `Guid.ToString(format)`'s four straightforward specifiers - N (no separators), D (hyphenated, the
   * shape already checked by {@link tryParseGuid}), B (braced) and P (parenthesized). `X` deliberately
   * not implemented: it renders as a C#-syntax initializer list, not a URL-shaped string, and nothing
   * about a route makes that a plausible thing to have ever asked for.
   */
  private formatGuid(guid: string, format: string): string | null {
    const digits = guid.replace(/-/g, '').toLowerCase();
    const dashed = `${digits.slice(0, 8)}-${digits.slice(8, 12)}-${digits.slice(12, 16)}-${digits.slice(16, 20)}-${digits.slice(20)}`;

    switch (format.toUpperCase()) {
      case 'N':
        return digits;
      case 'D':
        return dashed;
      case 'B':
        return `{${dashed}}`;
      case 'P':
        return `(${dashed})`;
      default:
        return null;
    }
  }

  /** Whether `value` is shaped like a plain, optionally negative, optionally decimal number. */
  private tryParseNumber(value: unknown): number | null {
    if (typeof value === 'number' && isFinite(value)) {
      return value;
    }

    return typeof value === 'string' && /^-?\d+(\.\d+)?$/.test(value) ? Number(value) : null;
  }

  /**
   * A representative slice of .NET's numeric format strings - the ones a URL path segment plausibly
   * asks for. Custom zero-padding (`"0000"`, unchanged from before) plus four standard specifiers, each
   * with an optional 1-2 digit precision: `D` (zero-padded, integers only - the standard-format spelling
   * of the same thing `"0000"` does), `X`/`x` (hexadecimal, integers only, letter case controls digit
   * case, negative not attempted - see {@link formatHex}'s remarks), `F` (fixed-point) and `N`
   * (fixed-point with thousands grouping). Deliberately not `C`/`P` (currency/percent): both put a
   * character that means something else in a URL - a locale-dependent currency symbol, or `%`, the URL
   * escape character itself - into the path, which nothing about routing would ever actually want. Not
   * `E`/`G`/`R` either: those are about round-trip print fidelity for an arbitrary number, not a shape
   * any real route has a reason to ask for.
   */
  private formatNumber(value: number, format: string): string | null {
    if (/^0+$/.test(format)) {
      return Number.isInteger(value) ? this.padInteger(value, format.length) : null;
    }

    const standard = /^([DdXxFfNn])(\d{0,2})$/.exec(format);
    if (!standard) {
      return null;
    }

    const [, specifier, precisionText] = standard;
    const precision = precisionText === '' ? null : Number(precisionText);

    switch (specifier.toUpperCase()) {
      case 'D':
        return Number.isInteger(value) ? this.padInteger(value, precision ?? 0) : null;
      case 'X':
        return Number.isInteger(value) && value >= 0 ? this.formatHex(value, precision ?? 0, specifier === 'X') : null;
      case 'F':
        return value.toFixed(precision ?? 2);
      case 'N':
        return this.groupThousands(value.toFixed(precision ?? 2));
      default:
        return null;
    }
  }

  private padInteger(value: number, width: number): string {
    const negative = value < 0;
    const padded = Math.abs(value).toString().padStart(width, '0');
    return negative ? `-${padded}` : padded;
  }

  /**
   * Negative values are rejected before this is ever called - .NET's `X` renders a negative signed
   * integer as its two's-complement bit pattern, which depends on the source type's own width (`int` vs
   * `long` vs `byte` produce different digits for the same value), information a form control's plain
   * number does not carry. Guessing a width would risk a preview that looks plausible and is wrong;
   * not attempting it is the same call already made for a Guid's `X` and for `C`/`P`/`E`/`G`/`R`.
   */
  private formatHex(value: number, width: number, uppercase: boolean): string {
    const hex = value.toString(16).padStart(width, '0');
    return uppercase ? hex.toUpperCase() : hex;
  }

  private groupThousands(fixedValue: string): string {
    const negative = fixedValue.startsWith('-');
    const [integerPart, fractionPart] = (negative ? fixedValue.slice(1) : fixedValue).split('.');
    const grouped = integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return `${negative ? '-' : ''}${grouped}${fractionPart ? '.' + fractionPart : ''}`;
  }
}
