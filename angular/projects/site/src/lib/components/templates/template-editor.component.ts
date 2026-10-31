import { CoreModule, PermissionService } from '@abp/ng.core';
import { ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import { DOCUMENT, NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, switchMap, tap } from 'rxjs';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import { FieldAdminService } from '../../proxy/dignite/site/admin/fields/field-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import { SiteTemplateAdminService } from '../../proxy/dignite/site/admin/templates/site-template-admin.service';
import type {
  SiteTemplatePageUsageDto,
  SiteTemplateUsageDto,
} from '../../proxy/dignite/site/admin/templates/models';
import { ContentStatus } from '../../proxy/dignite/site/contents/content-status.enum';
import type { ContentDto } from '../../proxy/dignite/site/contents/models';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { SiteTemplateKind } from '../../proxy/dignite/site/templates/site-template-kind.enum';
import type {
  SiteTemplateDto,
  SiteTemplateVersionDto,
} from '../../proxy/dignite/site/templates/models';
import { eSitePolicyNames } from '../../enums/policy-names';
import { CodeEditorComponent } from './code-editor.component';

/** Mirrors `SiteTemplateConsts.MaxNameLength` / `MaxCommentLength` (`src/Dignite.Site.Domain.Shared/Templates/SiteTemplateConsts.cs`). */
const MAX_NAME_LENGTH = 256;
const MAX_COMMENT_LENGTH = 256;

/** Mirrors `SiteTemplateConsts.NamePattern`, which is `PageConsts.TemplatePattern` (also mirrored by `pages.component.ts`). */
const NAME_PATTERN = /^\/?[A-Za-z0-9_][A-Za-z0-9_/-]*$/;

/** How many published contents of a slug page the preview offers; the newest come first. */
const PREVIEW_CONTENT_LIMIT = 100;

/** `SiteErrorCodes.TemplateSyntaxError`: the message carries one `(line,column): message` line per error. */
const SYNTAX_ERROR_CODE = 'Site:090002';

const SYNTAX_ERROR_LINE = /^\s*\((\d+)\s*,\s*(\d+)\)\s*:\s*(.*)$/;

/** The field type of a page's SEO block: edited in its own panel, never written into a template by hand. */
const SEO_FIELD_TYPE = 'Seo';

/** A field name Scriban can read as a member (`content.fields.title`); anything else needs the indexer form. */
const SCRIBAN_MEMBER_NAME = /^[A-Za-z_][A-Za-z0-9_]*$/;

const LAYOUT_SKELETON = `<!DOCTYPE html>
<html lang="{{ culture }}">
<head>
    <meta charset="utf-8" />
    <title>{{ head?.meta_title ?? page.display_name }}</title>
    {{ site_head }}
</head>
<body>
<nav>{{ for lang in languages }}<a href="{{ lang.url }}">{{ lang.culture_name }}</a> {{ end }}</nav>
{{ body }}
</body>
</html>
`;

const PARTIAL_SKELETON = `<div>
    {{ $1 }}
</div>
`;

const PAGE_SKELETON = `<h1>{{ page.display_name }}</h1>
{{ if content }}
    <p>{{ content.publish_time | date.to_string "%Y-%m-%d" }}</p>
    {{ for field in field_list }}
    <section>
        <h2>{{ field.display_name }}</h2>
        {{ render_field field.name }}
    </section>
    {{ end }}
{{ else }}
    {{ list = contents { max: 20 } }}
    <ul>
    {{ for item in list.items }}
        <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
    {{ end }}
    </ul>
{{ end }}
`;

/** Mirrors `SiteTemplateNames.Normalize`: trimmed, without a leading slash - how a template's name is stored. */
function normalizeTemplateName(name: string | null | undefined): string {
  return (name ?? '').trim().replace(/^\//, '');
}

/**
 * The source a new template starts with, by what its name says it is (the same reading as
 * `SiteTemplateNames`): the `layout`, a `partials/` fragment, an `assets/` file (not Scriban, so empty), or
 * otherwise a page template.
 */
export function skeletonFor(name: string): string {
  const normalized = normalizeTemplateName(name);

  if (normalized === 'layout') {
    return LAYOUT_SKELETON;
  }

  if (normalized.startsWith('partials/')) {
    return PARTIAL_SKELETON;
  }

  if (normalized.startsWith('assets/')) {
    return '';
  }

  return PAGE_SKELETON;
}

/**
 * The Scriban that prints a field of the current content. Plain values are read straight off
 * `content.fields`; a date is formatted; everything composite or rich (editor text, matrix, table, a linked
 * content, files) goes through `render_field`, which knows how to draw it. A name that is not a plain
 * identifier (it may hold a hyphen) is read with the indexer, which is the only form Scriban parses for it.
 */
export function snippetFor(fieldTypeName: string, name: string): string {
  const member = SCRIBAN_MEMBER_NAME.test(name) ? `content.fields.${name}` : `content.fields["${name}"]`;

  switch (fieldTypeName) {
    case 'Text':
    case 'Number':
    case 'Boolean':
    case 'Select':
    case 'Tree':
      return `{{ ${member} }}`;
    case 'DateTime':
      return `{{ ${member} | date.to_string "%Y-%m-%d" }}`;
    default:
      return `{{ render_field "${name}" }}`;
  }
}

/** One field in the sidebar: what the person sees and the snippet a click inserts. */
export interface AvailableField {
  name: string;
  displayName: string;
  fieldTypeName: string;
  snippet: string;
}

/** The fields of one content type of the page, in the order the content type arranges them. */
export interface AvailableFieldGroup {
  id: string;
  displayName: string;
  fields: AvailableField[];
}

/** One entry of a `Site:090002` error: where in the template source Scriban found the problem. */
export interface TemplateSyntaxError {
  line: number;
  column: number;
  message: string;
}

/** The part of an HTTP error response this screen reads: ABP's `{ error: { code, message, ... } }` body. */
interface ApiErrorResponse {
  message?: string;
  error?: {
    error?: {
      code?: string;
      message?: string;
      validationErrors?: { message?: string }[];
    };
  };
}

/**
 * Reads the `(line,column): message` lines of a `Site:090002` message. The first line is only the headline
 * ("The template 'X' has syntax errors:"), so any line that does not look like an entry is skipped.
 */
export function parseSyntaxErrors(message: string | null | undefined): TemplateSyntaxError[] {
  const errors: TemplateSyntaxError[] = [];

  for (const text of (message ?? '').split(/\r?\n/)) {
    const match = SYNTAX_ERROR_LINE.exec(text);
    if (match) {
      errors.push({ line: Number(match[1]), column: Number(match[2]), message: match[3].trim() });
    }
  }

  return errors;
}

function errorMessageOf(response: ApiErrorResponse | undefined): string {
  const error = response?.error?.error;
  const details = (error?.validationErrors ?? []).map(entry => entry.message).filter(Boolean);

  return [error?.message ?? response?.message, ...details].filter(Boolean).join('\n');
}

/**
 * Creating and editing one template, as a page of its own rather than a dialog: the source needs the room of
 * a code editor, and next to it sit the version history and the preview launcher.
 *
 * The preview is the real site in a new window, in draft mode: the admin API issues a short-lived link, and
 * opening it makes the public site render the chosen version in place of the published one. This screen only
 * picks what to open - a page of the site, one of its contents, or a hand-typed path.
 *
 * Saving always appends a version (the server never rewrites one). A draft is a version that is not the
 * published one; "save and publish" is a save that also moves the published pointer to the new version.
 */
@Component({
  selector: 'site-template-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './template-editor.component.html',
  imports: [
    CoreModule,
    ThemeSharedModule,
    ReactiveFormsModule,
    RouterLink,
    NgTemplateOutlet,
    CodeEditorComponent,
  ],
})
export class TemplateEditorComponent {
  private readonly templateService = inject(SiteTemplateAdminService);
  private readonly contentService = inject(ContentAdminService);
  private readonly pageService = inject(PageAdminService);
  private readonly fieldService = inject(FieldAdminService);
  private readonly permission = inject(PermissionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly toaster = inject(ToasterService);
  private readonly document = inject(DOCUMENT);

  readonly kinds = SiteTemplateKind;

  readonly templateId: string | null = this.route.snapshot.paramMap.get('id');
  readonly isEdit = !!this.templateId;

  /** Creating needs Create; everything on an existing template that writes (save, publish) needs Update. */
  readonly canSave = this.permission.getGrantedPolicy(
    this.isEdit ? eSitePolicyNames.TemplatesUpdate : eSitePolicyNames.TemplatesCreate,
  );

  // State that changes in an HTTP callback is a signal, not a plain field: the host layout's ancestors
  // (`abp-router-outlet`, the LeptonX layouts) are OnPush, and a plain-field write in a response callback
  // never marks them for check, so the screen would stay blank (or go stale) until some unrelated event
  // happened to.
  readonly isLoading = signal(this.isEdit);
  readonly isBusy = signal(false);
  readonly template = signal<SiteTemplateDto | undefined>(undefined);
  /** Newest first. */
  readonly versions = signal<SiteTemplateVersionDto[]>([]);

  /** The source in the code editor - unsaved edits included, which is why it is not read from a version. */
  readonly body = signal('');
  /** The version whose source was last loaded into the editor (highlighted in the table). */
  readonly loadedVersion = signal<number | undefined>(undefined);

  /** Set by a `Site:090002` save error; empty otherwise. */
  readonly syntaxErrors = signal<TemplateSyntaxError[]>([]);
  /** Set by any other save error. */
  readonly saveError = signal<string | null>(null);

  /** The version the preview renders. Starts as the one the editor was opened on. */
  readonly selectedVersion = signal<number | undefined>(undefined);

  /** Who uses this template: the pages it renders and, for a partial or an asset, the templates that refer to it. */
  readonly usage = signal<SiteTemplateUsageDto | undefined>(undefined);
  /** The page the preview opens; starts as the first page of {@link usage}. */
  readonly selectedPageId = signal<string | undefined>(undefined);
  /** Published contents of the selected page, newest first; empty unless the page route has a slug. */
  readonly contents = signal<ContentDto[]>([]);
  readonly selectedContentId = signal<string | undefined>(undefined);
  /** The "Advanced" path input; empty means it is not used. */
  readonly customPath = signal('');
  /** Set when the server refuses to issue a preview link. */
  readonly previewLinkError = signal<string | null>(null);

  /** Create mode, from `?page=`: the page the new template is for. Unset when the query names none, or no such page. */
  readonly forPage = signal<PageDto | undefined>(undefined);
  /** The page whose content types the field sidebar lists: the preview's page when editing, {@link forPage} when creating. */
  readonly sidebarPage = signal<PageDto | undefined>(undefined);
  /** The whole field library, read once; `undefined` until it has been asked for, empty when it could not be read. */
  private readonly fieldLibrary = signal<FieldDto[] | undefined>(undefined);
  private fieldLibraryRequested = false;

  /** One group per content type of the sidebar page, with its fields in arrangement order and without the SEO block. */
  readonly fieldGroups = computed<AvailableFieldGroup[]>(() => {
    const page = this.sidebarPage();
    const library = this.fieldLibrary();

    if (!page || !library) {
      return [];
    }

    const fieldsById = new Map(library.map(field => [field.id, field] as const));

    return (page.contentTypes ?? [])
      .map(contentType => ({
        id: contentType.id ?? contentType.name ?? '',
        displayName: contentType.displayName || contentType.name || '',
        fields: [...(contentType.fields ?? [])]
          .sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
          .flatMap(arranged => {
            const field = fieldsById.get(arranged.fieldId ?? '');
            if (!field?.name || field.fieldTypeName === SEO_FIELD_TYPE) {
              return [];
            }

            const fieldTypeName = field.fieldTypeName ?? '';
            return [
              {
                name: field.name,
                displayName: arranged.displayName || field.displayName || field.name,
                fieldTypeName,
                snippet: snippetFor(fieldTypeName, field.name),
              },
            ];
          }),
      }))
      .filter(group => group.fields.length > 0);
  });

  /** The right column exists when there is something to put in it: the preview (edit), or a page's fields (create). */
  readonly showFieldsColumn = computed(() => this.isEdit || !!this.forPage());

  readonly isSelectedVersionPublished = computed(() => {
    const selected = this.selectedVersion();
    return selected !== undefined && selected === this.template()?.publishedVersionNumber;
  });

  readonly pages = computed<SiteTemplatePageUsageDto[]>(() => this.usage()?.pages ?? []);

  readonly selectedPage = computed(() => this.pages().find(page => page.pageId === this.selectedPageId()));

  readonly selectedContent = computed(() =>
    this.contents().find(content => content.id === this.selectedContentId()),
  );

  /** Names of the templates that refer to this one with `include` / `asset_url` (partials and assets). */
  readonly referencedByNames = computed(() => {
    const names = (this.usage()?.referencedBy ?? []).map(reference => reference.templateName);
    return [...new Set(names)].join(', ');
  });

  /** The address the preview opens: a typed path, else the selected content, else the selected page. */
  readonly previewPath = computed(
    () => this.customPath().trim() || this.selectedContent()?.url || this.selectedPage()?.path || '/',
  );

  readonly form: FormGroup = this.buildForm();

  @ViewChild(CodeEditorComponent) codeEditor?: CodeEditorComponent;

  constructor() {
    if (this.templateId) {
      this.loadInitial(this.templateId);
    } else {
      this.prefillFromQuery();
    }
  }

  /** Inserts a field's snippet into the editor at the cursor. */
  insertField(field: AvailableField): void {
    this.codeEditor?.insert(field.snippet);
  }

  save(publish: boolean): void {
    if (!this.canSave || this.isBusy()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isBusy.set(true);

    const value = this.form.getRawValue();
    const input = {
      body: this.body(),
      comment: (value.comment ?? '').trim() || undefined,
      publish,
    };

    // skipHandleError: a syntax error is shown beside the editor, with a way to jump to each line, which
    // the global handler's toast cannot do - and it would only be a second, redundant message.
    const request$ = this.templateId
      ? this.templateService.update(this.templateId, input, { skipHandleError: true })
      : this.templateService.create({ name: value.name, ...input }, { skipHandleError: true });

    request$.subscribe({
      next: saved => this.onSaved(saved, publish),
      error: (error: ApiErrorResponse) => this.onSaveError(error),
    });
  }

  /** Makes a saved version the published one, from the versions table. */
  publishVersion(versionNumber: number): void {
    if (!this.templateId || !this.canSave || this.isBusy()) {
      return;
    }

    const id = this.templateId;
    this.isBusy.set(true);

    this.templateService.publish(id, versionNumber).subscribe({
      next: () => {
        this.toaster.success('Site::TemplatePublished', undefined, {
          messageLocalizationParams: [String(versionNumber)],
        });
        this.fetch(id).subscribe({
          next: () => this.isBusy.set(false),
          error: () => this.isBusy.set(false),
        });
      },
      error: () => this.isBusy.set(false),
    });
  }

  /** Loads an old version's source into the editor, so it can be edited into a new version. */
  loadVersion(versionNumber: number): void {
    if (!this.templateId) {
      return;
    }

    this.templateService.getVersion(this.templateId, versionNumber).subscribe(version => {
      this.body.set(version.body);
      this.loadedVersion.set(version.number);
    });
  }

  /** Chooses the page the preview opens, and loads its contents when its route has a slug. */
  selectPage(pageId: string | undefined): void {
    this.selectedPageId.set(pageId);
    this.contents.set([]);
    this.selectedContentId.set(undefined);
    this.loadSidebarPage(pageId);

    const page = this.selectedPage();
    if (!page?.hasSlug) {
      return;
    }

    this.contentService
      .getList({
        pageId: page.pageId,
        status: ContentStatus.Published,
        maxResultCount: PREVIEW_CONTENT_LIMIT,
        sorting: 'publishTime desc',
      })
      .subscribe(result => {
        // The answer of an earlier choice arriving after a newer one must not overwrite it.
        if (this.selectedPageId() !== page.pageId) {
          return;
        }

        const items = result.items ?? [];
        const sampleId = page.sampleContent?.contentId;

        this.contents.set(items);
        this.selectedContentId.set((items.find(item => item.id === sampleId) ?? items[0])?.id);
      });
  }

  /**
   * Opens the selected version on the site, in a new window, in draft mode.
   *
   * The window is opened first, synchronously inside the click: browsers allow a popup only in direct
   * response to a user action, and the link only arrives after an HTTP round trip. The blank window is then
   * pointed at the link, or closed again when no link could be issued.
   */
  openPreview(versionNumber: number | undefined = this.selectedVersion()): void {
    const id = this.templateId;

    if (!id || versionNumber === undefined) {
      return;
    }

    const win = this.document.defaultView?.open('', '_blank') ?? null;
    if (win) {
      // The previewed site is another origin; it has no business reaching back into the admin.
      win.opener = null;
    }

    this.selectedVersion.set(versionNumber);
    this.previewLinkError.set(null);

    // skipHandleError: the message is shown beside the button, where the person is looking.
    this.templateService
      .getPreviewLink(id, versionNumber, this.previewPath(), { skipHandleError: true })
      .subscribe({
        next: link => {
          if (win) {
            win.location.href = link.url;
          } else {
            // The blank window was blocked; a navigation outside the click may be blocked as well, but costs nothing.
            this.document.defaultView?.open(link.url, '_blank');
          }
        },
        error: (error: ApiErrorResponse) => {
          win?.close();
          this.previewLinkError.set(errorMessageOf(error));
        },
      });
  }

  /** Moves the editor's cursor to where a syntax error was found. */
  goToError(error: TemplateSyntaxError): void {
    this.codeEditor?.goTo(error.line, error.column);
  }

  /**
   * Create mode, from the address (`/site/templates/create?name=Blog/Post&page=blog`, which the page form's
   * "create a template for this page" link builds): the name, a starting source for what that name is, and the
   * page whose fields the sidebar lists. Nothing is prefilled when the address names neither.
   */
  private prefillFromQuery(): void {
    const query = this.route.snapshot.queryParamMap;
    const name = normalizeTemplateName(query.get('name'));
    const pageName = (query.get('page') ?? '').trim();

    if (name) {
      this.form.get('name')?.setValue(name);
    }

    if (name || pageName) {
      this.body.set(skeletonFor(name));
    }

    if (pageName) {
      // skipHandleError: a stale link to a page that is gone just means no hint and no sidebar.
      this.pageService.findByName(pageName, { skipHandleError: true }).subscribe({
        next: page => {
          this.forPage.set(page);
          this.sidebarPage.set(page);
          this.ensureFieldLibrary();
        },
        error: () => this.forPage.set(undefined),
      });
    }
  }

  /** Edit mode: the sidebar follows the preview's page. */
  private loadSidebarPage(pageId: string | undefined): void {
    this.sidebarPage.set(undefined);

    if (!pageId) {
      return;
    }

    this.ensureFieldLibrary();

    // skipHandleError: the sidebar is an aid; a failure to fill it is not worth a toast over the editor.
    this.pageService.get(pageId, { skipHandleError: true }).subscribe({
      next: page => {
        // The answer of an earlier choice arriving after a newer one must not overwrite it.
        if (this.selectedPageId() === pageId) {
          this.sidebarPage.set(page);
        }
      },
      error: () => this.sidebarPage.set(undefined),
    });
  }

  /** Reads the field library the first time a page's fields are needed, and keeps it for the screen's life. */
  private ensureFieldLibrary(): void {
    if (this.fieldLibraryRequested) {
      return;
    }

    this.fieldLibraryRequested = true;

    // skipHandleError: without the Fields permission the sidebar just stays empty.
    this.fieldService.getList({}, { skipHandleError: true }).subscribe({
      next: result => this.fieldLibrary.set(result.items ?? []),
      error: () => this.fieldLibrary.set([]),
    });
  }

  private onSaved(saved: SiteTemplateDto, publish: boolean): void {
    this.isBusy.set(false);
    this.syntaxErrors.set([]);
    this.saveError.set(null);

    const versionNumber = publish
      ? (saved.publishedVersionNumber ?? saved.latestVersionNumber)
      : saved.latestVersionNumber;

    this.toaster.success(publish ? 'Site::TemplatePublished' : 'Site::TemplateSaved', undefined, {
      messageLocalizationParams: [String(versionNumber)],
    });

    if (!this.templateId) {
      this.router.navigate(['/site/templates', saved.id, 'edit']);
      return;
    }

    // The editor already holds what was saved, so only the history is reloaded - and the preview moves to
    // the new version, since seeing it is what saving is for. No window is opened: that is the button's job.
    this.fetch(this.templateId).subscribe(() => {
      this.loadedVersion.set(saved.latestVersionNumber);
      this.selectedVersion.set(saved.latestVersionNumber);
      this.form.get('comment')?.reset('');
    });
  }

  private onSaveError(response: ApiErrorResponse): void {
    this.isBusy.set(false);

    const error = response?.error?.error;
    if (error?.code === SYNTAX_ERROR_CODE) {
      const parsed = parseSyntaxErrors(error.message);
      if (parsed.length > 0) {
        this.syntaxErrors.set(parsed);
        this.saveError.set(null);
        return;
      }
    }

    this.syntaxErrors.set([]);
    this.saveError.set(errorMessageOf(response));
  }

  /** Opens on the published version's source, or the latest one when nothing is published. */
  private loadInitial(id: string): void {
    this.fetch(id)
      .pipe(
        switchMap(({ template }) => {
          const versionNumber = template.publishedVersionNumber ?? template.latestVersionNumber;
          this.selectedVersion.set(versionNumber);
          return this.templateService.getVersion(id, versionNumber);
        }),
      )
      .subscribe({
        next: version => {
          this.body.set(version.body);
          this.loadedVersion.set(version.number);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false),
      });
  }

  private fetch(id: string) {
    return forkJoin({
      template: this.templateService.get(id),
      versions: this.templateService.getVersions(id),
      usage: this.templateService.getUsage(id),
    }).pipe(
      tap(({ template, versions, usage }) => {
        this.template.set(template);
        this.versions.set([...(versions.items ?? [])].sort((a, b) => b.number - a.number));
        this.usage.set(usage);

        // Keep the person's page choice across a reload (publishing a version fetches again); fall back to
        // the first page when there was none or it no longer uses this template.
        const pages = usage.pages ?? [];
        if (!pages.some(page => page.pageId === this.selectedPageId())) {
          this.selectPage(pages[0]?.pageId);
        }
      }),
    );
  }

  private buildForm(): FormGroup {
    const form: FormGroup = this.fb.group({
      comment: ['', [Validators.maxLength(MAX_COMMENT_LENGTH)]],
    });

    // The name is fixed once the template exists: pages refer to it, so renaming would silently unlink them.
    if (!this.isEdit) {
      form.addControl(
        'name',
        this.fb.control('', [
          Validators.required,
          Validators.maxLength(MAX_NAME_LENGTH),
          Validators.pattern(NAME_PATTERN),
        ]),
      );
    }

    if (!this.canSave) {
      form.disable();
    }

    return form;
  }
}
