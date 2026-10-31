import { CoreModule, PermissionService } from '@abp/ng.core';
import { ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import { DOCUMENT } from '@angular/common';
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
import { SiteTemplateAdminService } from '../../proxy/dignite/site/admin/templates/site-template-admin.service';
import type {
  SiteTemplatePageUsageDto,
  SiteTemplateUsageDto,
} from '../../proxy/dignite/site/admin/templates/models';
import { ContentStatus } from '../../proxy/dignite/site/contents/content-status.enum';
import type { ContentDto } from '../../proxy/dignite/site/contents/models';
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
  imports: [CoreModule, ThemeSharedModule, ReactiveFormsModule, RouterLink, CodeEditorComponent],
})
export class TemplateEditorComponent {
  private readonly templateService = inject(SiteTemplateAdminService);
  private readonly contentService = inject(ContentAdminService);
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
    }
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
