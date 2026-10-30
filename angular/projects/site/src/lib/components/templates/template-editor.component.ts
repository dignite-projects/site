import { CoreModule, PermissionService } from '@abp/ng.core';
import { ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import {
  ChangeDetectionStrategy,
  Component,
  ViewChild,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { DomSanitizer } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, switchMap, tap } from 'rxjs';
import { SiteTemplateAdminService } from '../../proxy/dignite/site/admin/templates/site-template-admin.service';
import type {
  SiteTemplateDto,
  SiteTemplatePreviewDto,
  SiteTemplateVersionDto,
} from '../../proxy/dignite/site/templates/models';
import { eSitePolicyNames } from '../../enums/policy-names';
import { SiteFileUrlService } from '../../files/services/site-file-url.service';
import { CodeEditorComponent } from './code-editor.component';

/** Mirrors `SiteTemplateConsts.MaxNameLength` / `MaxCommentLength` (`src/Dignite.Site.Domain.Shared/Templates/SiteTemplateConsts.cs`). */
const MAX_NAME_LENGTH = 256;
const MAX_COMMENT_LENGTH = 256;

/** Mirrors `SiteTemplateConsts.NamePattern`, which is `PageConsts.TemplatePattern` (also mirrored by `pages.component.ts`). */
const NAME_PATTERN = /^\/?[A-Za-z0-9_][A-Za-z0-9_/-]*$/;

/** Mirrors `SiteTemplateConsts.LayoutName`: the template that wraps every page. */
const LAYOUT_NAME = 'layout';

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

/**
 * Puts `<base href>` right after the opening `<head>` tag, or in front of the document when there is none -
 * see {@link TemplateEditorComponent.renderPreview} for why the preview needs it.
 */
export function withBaseHref(html: string, baseHref: string): string {
  if (!baseHref) {
    return html;
  }

  const base = `<base href="${baseHref.replace(/&/g, '&amp;').replace(/"/g, '&quot;')}">`;
  const head = /<head(\s[^>]*)?>/i;

  // A replacer function, not a replacement string: the rendered page may carry `$` sequences that
  // String.replace would otherwise expand.
  return head.test(html) ? html.replace(head, opening => opening + base) : base + html;
}

function errorMessageOf(response: ApiErrorResponse | undefined): string {
  const error = response?.error?.error;
  const details = (error?.validationErrors ?? []).map(entry => entry.message).filter(Boolean);

  return [error?.message ?? response?.message, ...details].filter(Boolean).join('\n');
}

function withoutLeadingSlash(name: string): string {
  return name.replace(/^\//, '');
}

/**
 * Creating and editing one template, as a page of its own rather than a dialog: the source needs the room of
 * a code editor, and next to it sit the version history and a live preview.
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
  private readonly fileUrl = inject(SiteFileUrlService);
  private readonly permission = inject(PermissionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly toaster = inject(ToasterService);
  private readonly sanitizer = inject(DomSanitizer);

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

  readonly previewPath = signal('/');
  /** The version the preview renders. Starts as the one the editor was opened on. */
  readonly selectedVersion = signal<number | undefined>(undefined);
  readonly preview = signal<SiteTemplatePreviewDto | undefined>(undefined);
  readonly previewSrcdoc = signal('');
  readonly previewError = signal<string | null>(null);
  readonly isPreviewing = signal(false);

  /**
   * The preview document as the iframe's `srcdoc` takes it. An `[srcdoc]` binding is an HTML sink, and
   * Angular's sanitizer would strip `<head>`, `<style>` and `<base>` from a plain string - exactly what a
   * rendered page is made of. Marking it trusted is safe here because the iframe is sandboxed without
   * `allow-scripts`: nothing in a template's output can run.
   */
  readonly previewDocument = computed(() => this.sanitizer.bypassSecurityTrustHtml(this.previewSrcdoc()));

  readonly isSelectedVersionPublished = computed(() => {
    const selected = this.selectedVersion();
    return selected !== undefined && selected === this.template()?.publishedVersionNumber;
  });

  /**
   * The template a request for the previewed path would normally use, when that is not the template being
   * edited - the preview then shows this template standing in for it. `layout` wraps every page, so it
   * never needs the note.
   */
  readonly pageTemplateNote = computed(() => {
    const preview = this.preview();
    const template = this.template();

    if (!preview || !template || template.name === LAYOUT_NAME) {
      return null;
    }

    return withoutLeadingSlash(preview.pageTemplate) !== withoutLeadingSlash(template.name)
      ? preview.pageTemplate
      : null;
  });

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

  /** Points the preview at a saved version and renders it. */
  previewVersion(versionNumber: number): void {
    this.selectedVersion.set(versionNumber);
    this.renderPreview();
  }

  /**
   * Renders the selected version on the path the preview box holds.
   *
   * The result is shown through the iframe's `srcdoc`, not a `src` pointing at the API: an iframe request
   * cannot carry the bearer token. The page's relative addresses (images, CSS, files) must then resolve
   * against the host that serves the site, which in this product is the `SiteAdmin` API's host - hence the
   * `<base>` element, the same host the file previews put in front of a stored address.
   */
  renderPreview(): void {
    const id = this.templateId;
    const version = this.selectedVersion();

    if (!id || version === undefined) {
      return;
    }

    this.isPreviewing.set(true);
    this.previewError.set(null);

    this.templateService
      .preview(id, version, this.previewPath().trim() || '/', { skipHandleError: true })
      .subscribe({
        next: result => {
          this.preview.set(result);
          this.previewSrcdoc.set(withBaseHref(result.html, this.baseHref()));
          this.isPreviewing.set(false);
        },
        error: (error: ApiErrorResponse) => {
          this.preview.set(undefined);
          this.previewSrcdoc.set('');
          this.previewError.set(errorMessageOf(error));
          this.isPreviewing.set(false);
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
    // the new version, since seeing it is what saving is for.
    this.fetch(this.templateId).subscribe(() => {
      this.loadedVersion.set(saved.latestVersionNumber);
      this.selectedVersion.set(saved.latestVersionNumber);
      this.form.get('comment')?.reset('');
      this.renderPreview();
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
    }).pipe(
      tap(({ template, versions }) => {
        this.template.set(template);
        this.versions.set([...(versions.items ?? [])].sort((a, b) => b.number - a.number));
      }),
    );
  }

  private baseHref(): string {
    const apiBase = this.fileUrl.apiBase;
    return apiBase ? `${apiBase}/` : '';
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
