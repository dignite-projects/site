import { CoreModule, LocalizationService, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  OnInit,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { FieldAdminService } from '../../proxy/dignite/site/admin/fields/field-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import { SiteThemeAdminService } from '../../proxy/dignite/site/admin/themes/site-theme-admin.service';
import { SiteThemeTransferAdminService } from '../../proxy/dignite/site/admin/themes/site-theme-transfer-admin.service';
import type {
  SiteReleaseDto,
  SiteThemeCheckDto,
  SiteThemeDto,
  SiteThemeFileDto,
  SiteThemeFileSummaryDto,
  SiteThemeFileUsagesDto,
  SiteThemeImportResultDto,
  SiteThemeIssueDto,
  SiteThemeSyntaxErrorDto,
} from '../../proxy/dignite/site/admin/themes/models';
import { SiteThemeUsageKindDto } from '../../proxy/dignite/site/admin/themes/site-theme-usage-kind-dto.enum';
import { SiteThemeFileStatus } from '../../proxy/dignite/site/themes/site-theme-file-status.enum';
import { SiteThemeIssueSeverity } from '../../proxy/dignite/site/themes/site-theme-issue-severity.enum';
import { eSitePolicyNames } from '../../enums/policy-names';
import { CodeEditorComponent } from './code-editor.component';
import {
  ApiErrorResponse,
  ThemeTreeNode,
  buildThemeTree,
  ThemeField,
  errorMessageOf,
  fieldGroupsFor,
  foldersOf,
  isAssetPath,
  languageOf,
  normalizeThemePath,
  pageTemplatePath,
  skeletonFor,
  themePathProblem,
} from './theme-workbench.helpers';

/** `Site:090003`: the working copy changed since the check the publish was based on. */
const CHANGED_SINCE_CHECK = 'Site:090003';

/**
 * A file open in a tab: what the editor shows, and what was last saved, to tell whether it has unsaved changes.
 * A file only looked at is not `pinned`: opening another file replaces its tab. One that was edited, or created, is,
 * and stays until it is closed.
 */
export interface ThemeOpenFile {
  path: string;
  isAsset: boolean;
  body: string;
  savedBody: string;
  pinned: boolean;
  concurrencyStamp?: string;
  syntaxErrors: SiteThemeSyntaxErrorDto[];
}

/** A page the template in front renders. */
export interface ThemeTemplatePage {
  id: string;
  name: string;
}

/** A row of the tree as the template renders it: a node and how deep it is. */
interface ThemeTreeRow {
  node: ThemeTreeNode;
  depth: number;
}

/**
 * The theme workbench: the working copy's files in a tree on the left, open files in an editor in the middle, the
 * check on the right, and publishing, the release history and the theme's settings above.
 *
 * Saving writes one file of the working copy in place and changes nothing on the site; publishing freezes the whole
 * working copy into a release and makes it live. See the Scriban Templates page of the documentation for the model.
 *
 * Deliberately no `<form (ngSubmit)>` around the editor: ABP's `FormSubmitDirective` (pulled in by `CoreModule`)
 * submits a form on every Enter keyup that does not come from a `<textarea>`, and CodeMirror is a contenteditable.
 */
@Component({
  selector: 'site-theme-workbench',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './theme-workbench.component.html',
  imports: [CoreModule, ThemeSharedModule, FormsModule, CodeEditorComponent],
  // The three columns are sized to the viewport, so that the page itself does not scroll: the editor keeps one
  // height whichever file is open, and the tree and the findings scroll inside their columns. The rem offsets are
  // what the LeptonX layout, the header card and the column headers take above and below them.
  styles: `
    :host {
      --site-code-editor-height: max(24rem, calc(100vh - 25rem));
      --site-code-editor-max-height: none;
    }

    .theme-tree {
      max-height: max(20rem, calc(100vh - 21rem));
      overflow-y: auto;
    }

    .theme-tree-row {
      display: flex;
      align-items: center;
      width: 100%;
      padding-right: 0.25rem;
      border-radius: var(--bs-border-radius-sm, 0.25rem);
    }

    .theme-tree-main {
      display: flex;
      align-items: center;
      gap: 0.25rem;
      flex: 1 1 auto;
      min-width: 0;
      border: none;
      background: transparent;
      text-align: start;
      padding: 0.15rem 0;
      color: inherit;
      white-space: nowrap;
    }

    .theme-tree-row:hover {
      background-color: rgba(128, 128, 128, 0.12);
    }

    /* The "more" button shows on the row that is pointed at, focused or has its menu open - and always on a touch screen. */
    .theme-tree-more {
      flex: 0 0 auto;
      border: none;
      background: transparent;
      color: inherit;
      padding: 0.1rem 0.4rem;
      opacity: 0;
    }

    .theme-tree-row:hover .theme-tree-more,
    .theme-tree-row:focus-within .theme-tree-more,
    .theme-tree-more.open {
      opacity: 1;
    }

    @media (hover: none) {
      .theme-tree-more {
        opacity: 1;
      }
    }

    .theme-tree-row.active {
      background-color: rgba(var(--bs-primary-rgb, 13, 110, 253), 0.15);
    }

    .theme-tree-row .name {
      overflow: hidden;
      text-overflow: ellipsis;
      flex: 1 1 auto;
    }

    .theme-tree-row.deleted .name {
      text-decoration: line-through;
      opacity: 0.6;
    }

    .theme-tab {
      white-space: nowrap;
    }

    .theme-field-snippet {
      white-space: pre-wrap;
      word-break: break-all;
    }

    .theme-tabs {
      overflow-x: auto;
      overflow-y: hidden;
    }

    .theme-issues {
      max-height: max(20rem, calc(100vh - 17rem));
      overflow-y: auto;
    }

    /* The fields scroll inside their own box, so that the check below them stays in reach. */
    .theme-fields {
      max-height: max(10rem, 38vh);
      overflow-y: auto;
    }
  `,
})
export class ThemeWorkbenchComponent implements OnInit {
  private readonly themeService = inject(SiteThemeAdminService);
  private readonly transferService = inject(SiteThemeTransferAdminService);
  private readonly pageService = inject(PageAdminService);
  private readonly fieldService = inject(FieldAdminService);
  private readonly permission = inject(PermissionService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly localization = inject(LocalizationService);

  readonly statuses = SiteThemeFileStatus;
  readonly severities = SiteThemeIssueSeverity;

  readonly canExport = this.permission.getGrantedPolicy(eSitePolicyNames.Themes);
  readonly canEdit = this.permission.getGrantedPolicy(eSitePolicyNames.ThemesEdit);
  readonly canPublish = this.permission.getGrantedPolicy(eSitePolicyNames.ThemesPublish);

  private readonly editor = viewChild(CodeEditorComponent);

  // State that changes in an HTTP callback is a signal, not a plain field: the host layout's ancestors are OnPush,
  // and a plain-field write in a response callback never marks them for check.
  readonly isLoading = signal(true);
  readonly isBusy = signal(false);
  readonly theme = signal<SiteThemeDto | undefined>(undefined);
  readonly files = signal<SiteThemeFileSummaryDto[]>([]);
  readonly collapsed = signal<ReadonlySet<string>>(new Set());
  readonly openFiles = signal<ThemeOpenFile[]>([]);
  readonly activePath = signal<string | undefined>(undefined);
  readonly check = signal<SiteThemeCheckDto | undefined>(undefined);
  readonly isChecking = signal(false);

  readonly tree = computed(() => buildThemeTree(this.files()));

  /** The tree flattened into the rows the template shows, folders that are collapsed left closed. */
  readonly treeRows = computed(() => {
    const rows: ThemeTreeRow[] = [];
    const collapsed = this.collapsed();

    const walk = (nodes: ThemeTreeNode[], depth: number) => {
      for (const node of nodes) {
        rows.push({ node, depth });
        if (node.isFolder && !collapsed.has(node.path)) {
          walk(node.children, depth + 1);
        }
      }
    };

    walk(this.tree(), 0);
    return rows;
  });

  readonly activeFile = computed(() => this.openFiles().find(file => file.path === this.activePath()));

  /** Whether the file in front can be the default layout: the last check lists it among the layouts. */
  readonly isLayoutInFront = computed(() => {
    const path = this.activePath();
    return !!path && (this.check()?.layouts ?? []).includes(path);
  });

  /** Whether the file in front already is the default layout. */
  readonly isDefaultLayoutInFront = computed(() => {
    const path = this.activePath();
    return !!path && path === this.theme()?.defaultLayout;
  });

  readonly hasUnsavedFiles = computed(() => this.openFiles().some(file => isDirty(file)));

  readonly errorCount = computed(() => this.issuesOf(SiteThemeIssueSeverity.Error).length);
  readonly warningCount = computed(() => this.issuesOf(SiteThemeIssueSeverity.Warning).length);

  // Dialogs.
  readonly isNewFileOpen = signal(false);
  readonly newFilePath = signal('');
  readonly newFileError = signal<string | null>(null);

  readonly isRenameOpen = signal(false);
  readonly renameFrom = signal('');
  readonly renameTo = signal('');
  readonly renameUpdatesReferences = signal(true);
  readonly renameError = signal<string | null>(null);

  readonly isPublishOpen = signal(false);
  readonly publishCheck = signal<SiteThemeCheckDto | undefined>(undefined);
  readonly publishComment = signal('');
  readonly publishError = signal<string | null>(null);

  readonly isImportOpen = signal(false);
  readonly importFile = signal<File | null>(null);
  /** Merge (false) or replace (true): whether the working copy loses the files the package does not have. */
  readonly importReplace = signal(false);
  readonly importResult = signal<SiteThemeImportResultDto | undefined>(undefined);

  readonly isReleasesOpen = signal(false);
  readonly releases = signal<SiteReleaseDto[]>([]);

  /** The "more" menu of a file in the tree: the file, and where it opens (fixed, so the tree's scrolling does not clip it). */
  readonly rowMenu = signal<{ path: string; top: number; right: number } | null>(null);

  /** The site's pages, to find those the template in front renders; empty without the Pages permission. */
  readonly pages = signal<PageDto[]>([]);

  /** The active pages whose template is the file in front. */
  readonly templatePages = computed<ThemeTemplatePage[]>(() => {
    const path = this.activePath();
    if (!path || isAssetPath(path)) {
      return [];
    }

    return this.pages()
      .filter(page => page.isActive && pageTemplatePath(page.template) === path)
      .map(page => ({ id: page.id ?? '', name: page.displayName || page.name }));
  });

  // The field sidebar.

  /** The page the author picked to list the fields of; the first page of the template in front otherwise. */
  readonly chosenFieldsPageId = signal<string | undefined>(undefined);
  /** Pages read in full, with their content types, by id; null for one that could not be read. */
  private readonly pageDetails = signal<ReadonlyMap<string, PageDto | null>>(new Map());
  /** The field library, read once; undefined until then, empty when it could not be read (no Fields permission). */
  private readonly fieldLibrary = signal<FieldDto[] | undefined>(undefined);
  private fieldLibraryRequested = false;
  private readonly requestedPageIds = new Set<string>();

  /** Whether the fields show: the file in front is the template of a page. The check shows whatever the file. */
  readonly showsFields = computed(() => this.templatePages().length > 0);

  readonly fieldsPageId = computed(() => {
    const ids = this.templatePages().map(page => page.id);
    const chosen = this.chosenFieldsPageId();
    return chosen && ids.includes(chosen) ? chosen : ids[0];
  });

  readonly fieldGroups = computed(() => {
    const id = this.fieldsPageId();
    return fieldGroupsFor((id && this.pageDetails().get(id)) || undefined, this.fieldLibrary());
  });

  readonly isLoadingFields = computed(() => {
    const id = this.fieldsPageId();
    return this.fieldLibrary() === undefined || (!!id && !this.pageDetails().has(id));
  });

  constructor() {
    // Reads what the sidebar lists when it is shown: the library once, each page once.
    effect(() => {
      if (!this.showsFields()) {
        return;
      }

      const id = this.fieldsPageId();
      untracked(() => {
        this.ensureFieldLibrary();
        if (id) {
          this.ensurePageDetail(id);
        }
      });
    });
  }

  ngOnInit(): void {
    forkJoin([this.themeService.get(), this.themeService.getFiles()]).subscribe({
      next: ([theme, files]) => {
        this.theme.set(theme);
        this.files.set(files.items ?? []);
        this.isLoading.set(false);
        this.runCheck();
      },
      error: () => this.isLoading.set(false),
    });

    if (this.permission.getGrantedPolicy(eSitePolicyNames.Pages)) {
      this.pageService.getList({}).subscribe(result => this.pages.set(result.items ?? []));
    }
  }

  /** Inserts a field's snippet into the file in front, at the cursor. */
  insertField(field: ThemeField): void {
    this.editor()?.insert(field.snippet);
  }

  private ensureFieldLibrary(): void {
    if (this.fieldLibraryRequested) {
      return;
    }

    this.fieldLibraryRequested = true;
    // skipHandleError: without the Fields permission the sidebar says it has nothing to list; no toast.
    this.fieldService.getList({}, { skipHandleError: true }).subscribe({
      next: result => this.fieldLibrary.set(result.items ?? []),
      error: () => this.fieldLibrary.set([]),
    });
  }

  private ensurePageDetail(id: string): void {
    if (this.requestedPageIds.has(id)) {
      return;
    }

    this.requestedPageIds.add(id);
    this.pageService.get(id, { skipHandleError: true }).subscribe({
      next: page => this.pageDetails.update(details => new Map(details).set(id, page)),
      error: () => this.pageDetails.update(details => new Map(details).set(id, null)),
    });
  }

  /** Opens the "more" menu of a file under its button, or closes it when it is already open for that file. */
  toggleRowMenu(event: MouseEvent, path: string): void {
    // Not on to the document listener below, which would close the menu at once.
    event.stopPropagation();
    if (this.rowMenu()?.path === path) {
      this.rowMenu.set(null);
      return;
    }

    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    this.rowMenu.set({ path, top: rect.bottom, right: window.innerWidth - rect.right });
  }

  /** Closes the "more" menu on a click anywhere, on Escape, and when the tree scrolls from under it. */
  @HostListener('document:click')
  @HostListener('document:keydown.escape')
  closeRowMenu(): void {
    if (this.rowMenu()) {
      this.rowMenu.set(null);
    }
  }

  renameFromMenu(): void {
    const path = this.rowMenu()?.path;
    this.rowMenu.set(null);
    if (path) {
      this.openRename(path);
    }
  }

  deleteFromMenu(): void {
    const path = this.rowMenu()?.path;
    this.rowMenu.set(null);
    if (path) {
      this.deleteFile(path);
    }
  }

  /** Asks before leaving the page while a file has unsaved changes. */
  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedFiles()) {
      event.preventDefault();
    }
  }

  /** Ctrl+S / Cmd+S saves the file in front, instead of the browser's "save page". */
  onEditorKeydown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
      event.preventDefault();
      this.save();
    }
  }

  isDirty(file: ThemeOpenFile): boolean {
    return isDirty(file);
  }

  statusKey(status: SiteThemeFileStatus): string | null {
    switch (status) {
      case SiteThemeFileStatus.Modified:
        return 'Site::ThemeFileStatus:Modified';
      case SiteThemeFileStatus.Added:
        return 'Site::ThemeFileStatus:Added';
      case SiteThemeFileStatus.Deleted:
        return 'Site::ThemeFileStatus:Deleted';
      default:
        return null;
    }
  }

  /** Whether the working copy's default layout is not the one the live release froze. */
  defaultLayoutChanged(check: SiteThemeCheckDto | undefined): boolean {
    return !!check?.liveReleaseNumber && (check.defaultLayout ?? '') !== (check.liveDefaultLayout ?? '');
  }

  issuesOf(severity: SiteThemeIssueSeverity): SiteThemeIssueDto[] {
    return (this.check()?.issues ?? []).filter(issue => issue.severity === severity);
  }

  toggleFolder(path: string): void {
    const next = new Set(this.collapsed());
    if (next.has(path)) {
      next.delete(path);
    } else {
      next.add(path);
    }

    this.collapsed.set(next);
  }

  select(node: ThemeTreeNode): void {
    if (node.isFolder) {
      this.toggleFolder(node.path);
    } else if (node.status !== SiteThemeFileStatus.Deleted) {
      this.openFile(node.path);
    }
  }

  openFile(path: string, then?: () => void): void {
    this.expandTo(path);

    if (this.openFiles().some(file => file.path === path)) {
      this.activePath.set(path);
      then?.();
      return;
    }

    this.themeService.getFile(path).subscribe(file => {
      // The tabs of files that were only looked at give way to this one.
      this.openFiles.update(files => [...files.filter(f => f.pinned || isDirty(f)), toOpenFile(file)]);
      this.activePath.set(file.path);
      // After the editor has taken the new document.
      setTimeout(() => then?.());
    });
  }

  closeFile(path: string): void {
    const file = this.openFiles().find(f => f.path === path);
    if (!file) {
      return;
    }

    const close = () => {
      const files = this.openFiles();
      const index = files.findIndex(f => f.path === path);
      const remaining = files.filter(f => f.path !== path);
      this.openFiles.set(remaining);

      if (this.activePath() === path) {
        this.activePath.set(remaining[Math.min(index, remaining.length - 1)]?.path);
      }
    };

    if (!isDirty(file)) {
      close();
      return;
    }

    this.confirmation
      .warn('Site::CloseUnsavedConfirm', 'AbpUi::AreYouSure', { messageLocalizationParams: [path] })
      .subscribe(status => {
        if (status === Confirmation.Status.confirm) {
          close();
        }
      });
  }

  onEdit(body: string): void {
    const path = this.activePath();
    this.openFiles.update(files =>
      files.map(file => (file.path === path ? { ...file, body, pinned: file.pinned || body !== file.savedBody } : file)));
  }

  save(overwrite = false): void {
    const file = this.activeFile();
    if (!file || !this.canEdit || this.isBusy()) {
      return;
    }

    this.isBusy.set(true);
    this.themeService
      .saveFile(
        { path: file.path, body: file.body, concurrencyStamp: overwrite ? undefined : file.concurrencyStamp },
        { skipHandleError: true },
      )
      .subscribe({
        next: saved => {
          this.isBusy.set(false);
          this.replaceOpenFile(file.path, {
            ...toOpenFile(saved),
            body: this.bodyOf(file.path) ?? saved.body ?? '',
            pinned: true,
          });
          this.toaster.success('Site::ThemeFileSaved');
          this.refreshFiles();
          this.runCheck();
        },
        error: (response: ApiErrorResponse) => {
          this.isBusy.set(false);
          if (response.status === 409) {
            this.resolveConflict(file.path);
            return;
          }

          this.toaster.error(errorMessageOf(response) || 'AbpUi::DefaultErrorMessage');
        },
      });
  }

  /** Someone saved the file after it was opened: overwrite their version with this one, or load theirs. */
  private resolveConflict(path: string): void {
    this.confirmation
      .warn('Site::SaveConflictConfirm', 'AbpUi::AreYouSure', {
        messageLocalizationParams: [path],
        yesText: 'Site::Overwrite',
        cancelText: 'Site::ReloadFile',
      })
      .subscribe(status => {
        // Closing the dialog (Esc, the close button) decides neither: the edits stay, unsaved.
        if (status === Confirmation.Status.confirm) {
          this.save(true);
        } else if (status === Confirmation.Status.reject) {
          this.reloadFile(path);
        }
      });
  }

  private reloadFile(path: string): void {
    this.themeService.getFile(path).subscribe(file => {
      this.replaceOpenFile(path, toOpenFile(file));
      this.toaster.info('Site::ThemeFileReloaded', undefined, { messageLocalizationParams: [path] });
    });
  }

  goToIssue(issue: SiteThemeIssueDto): void {
    if (!issue.path) {
      return;
    }

    const line = issue.line;
    const column = issue.column ?? 1;
    this.openFile(issue.path, () => {
      if (line) {
        this.editor()?.goTo(line, column);
      }
    });
  }

  goToSyntaxError(error: SiteThemeSyntaxErrorDto): void {
    this.editor()?.goTo(error.line, error.column);
  }

  /** The number of the check asked for last: only its answer counts, so an older one arriving late cannot overwrite it. */
  private checkSequence = 0;
  /** Callbacks waiting for the answer of the check asked for last, including those of checks it took over from. */
  private pendingAfter: ((check: SiteThemeCheckDto) => void)[] = [];

  /**
   * Asks for the check of the working copy as it is saved now. Checks run after every change, so one can be asked for
   * while another is still on its way; only the last one counts - the working copy it read is the newest. `after`
   * runs with that answer, also when a later check took this one's place.
   */
  runCheck(after?: (check: SiteThemeCheckDto) => void): void {
    const sequence = ++this.checkSequence;
    if (after) {
      this.pendingAfter.push(after);
    }

    this.isChecking.set(true);
    this.themeService.check().subscribe({
      next: check => {
        if (sequence !== this.checkSequence) {
          return;
        }

        this.check.set(check);
        this.isChecking.set(false);
        const callbacks = this.pendingAfter;
        this.pendingAfter = [];
        callbacks.forEach(callback => callback(check));
      },
      error: () => {
        if (sequence === this.checkSequence) {
          this.isChecking.set(false);
          this.pendingAfter = [];
        }
      },
    });
  }

  // New file.

  openNewFile(folder?: string): void {
    this.newFilePath.set(folder ? folder + '/' : '');
    this.newFileError.set(null);
    this.isNewFileOpen.set(true);
  }

  createFile(): void {
    const path = normalizeThemePath(this.newFilePath());
    const problem = themePathProblem(path);
    if (problem) {
      this.newFileError.set(this.localization.instant(problem));
      return;
    }

    if (this.files().some(file => file.path === path && file.status !== SiteThemeFileStatus.Deleted)) {
      this.newFileError.set(this.localization.instant('Site::ThemeFileExists', path));
      return;
    }

    this.isBusy.set(true);
    this.themeService.saveFile({ path, body: skeletonFor(path) }, { skipHandleError: true }).subscribe({
      next: saved => {
        this.isBusy.set(false);
        this.isNewFileOpen.set(false);
        this.refreshFiles();
        this.runCheck();
        this.openFiles.update(files => [...files.filter(f => f.path !== saved.path), { ...toOpenFile(saved), pinned: true }]);
        this.activePath.set(saved.path);
        this.expandTo(saved.path ?? '');
      },
      error: (response: ApiErrorResponse) => {
        this.isBusy.set(false);
        this.newFileError.set(errorMessageOf(response));
      },
    });
  }

  // Rename.

  openRename(path: string): void {
    this.renameFrom.set(path);
    this.renameTo.set(path);
    this.renameUpdatesReferences.set(true);
    this.renameError.set(null);
    this.isRenameOpen.set(true);
  }

  rename(): void {
    const from = this.renameFrom();
    const to = normalizeThemePath(this.renameTo());
    const problem = themePathProblem(to);
    if (problem) {
      this.renameError.set(this.localization.instant(problem));
      return;
    }

    if (this.openFiles().some(file => isDirty(file) && file.path === from)) {
      this.renameError.set(this.localization.instant('Site::SaveBeforeRename'));
      return;
    }

    this.isBusy.set(true);
    this.themeService
      .renameFile({ path: from, newPath: to, updateReferences: this.renameUpdatesReferences() }, { skipHandleError: true })
      .subscribe({
        next: result => {
          this.isBusy.set(false);
          this.isRenameOpen.set(false);

          // The renamed file keeps its tab, under its new path; files whose references were rewritten are reloaded
          // when they have no unsaved changes of their own.
          this.openFiles.update(files =>
            files.map(file => (file.path === from ? { ...toOpenFile(result.file), pinned: file.pinned } : file)));
          if (this.activePath() === from) {
            this.activePath.set(result.file.path);
          }

          for (const path of result.updatedPaths ?? []) {
            const open = this.openFiles().find(file => file.path === path);
            if (open && !isDirty(open) && path !== result.file.path) {
              this.reloadFile(path);
            }
          }

          this.toaster.success('Site::ThemeFileRenamed', undefined, {
            messageLocalizationParams: [String(result.updatedPaths?.length ?? 0)],
          });
          if (result.pagesStillUsingOldPath?.length) {
            this.toaster.warn('Site::RenamePagesStillUsing', undefined, {
              messageLocalizationParams: [result.pagesStillUsingOldPath.join(', ')],
            });
          }

          this.refreshTheme();
          this.refreshFiles();
          this.runCheck();
        },
        error: (response: ApiErrorResponse) => {
          this.isBusy.set(false);
          this.renameError.set(errorMessageOf(response));
        },
      });
  }

  // Delete.

  deleteFile(path: string): void {
    this.themeService.getFileUsages(path).subscribe(usages => {
      const users = describeUsages(usages, kind => this.localization.instant('Site::ThemeUsage:' + SiteThemeUsageKindDto[kind]));
      const confirm$ = users
        ? this.confirmation.warn('Site::DeleteThemeFileUsedConfirm', 'AbpUi::AreYouSure', {
            messageLocalizationParams: [path, users],
          })
        : this.confirmation.warn('Site::DeleteThemeFileConfirm', 'AbpUi::AreYouSure', {
            messageLocalizationParams: [path],
          });

      confirm$.subscribe(status => {
        if (status !== Confirmation.Status.confirm) {
          return;
        }

        this.themeService.deleteFile(path).subscribe(() => {
          this.openFiles.update(files => files.filter(file => file.path !== path));
          if (this.activePath() === path) {
            this.activePath.set(this.openFiles()[0]?.path);
          }

          this.toaster.success('AbpUi::SuccessfullyDeleted');
          this.refreshFiles();
          this.runCheck();
        });
      });
    });
  }

  // Preview.

  /**
   * Opens the site's home page in a new tab with the working copy as saved, or with a release, for this browser only;
   * the author follows the site's links from there. The tab is opened inside the click, so the browser lets it
   * through, and pointed at the signed link once it arrives; it gets no handle on this page, since the theme's scripts
   * run in it.
   */
  openPreview(releaseId?: string): void {
    const tab = window.open('', '_blank');
    if (!tab) {
      this.toaster.warn('Site::PreviewPopupBlocked');
      return;
    }

    tab.opener = null;
    if (!releaseId && this.hasUnsavedFiles()) {
      this.toaster.info('Site::PreviewUnsavedNotShown');
    }

    this.themeService.getPreviewLink(undefined, releaseId).subscribe({
      next: link => tab.location.replace(link.url!),
      // The error itself is shown by the default handler.
      error: () => tab.close(),
    });
  }

  // Publish.

  openPublish(): void {
    this.publishComment.set('');
    this.publishError.set(null);
    this.publishCheck.set(undefined);
    this.isPublishOpen.set(true);
    this.runCheck(check => this.publishCheck.set(check));
  }

  /** Enter in the comment publishes, unless it only confirms a word in an input method (Chinese, Japanese). */
  publishOnEnter(event: Event): void {
    if (!(event as KeyboardEvent).isComposing) {
      this.publish();
    }
  }

  publish(): void {
    const check = this.publishCheck();
    if (!check?.fingerprint || check.hasErrors || !check.hasChanges || this.isBusy()) {
      return;
    }

    this.isBusy.set(true);
    this.themeService
      .publish({ fingerprint: check.fingerprint, comment: this.publishComment() || undefined }, { skipHandleError: true })
      .subscribe({
        next: release => {
          this.isBusy.set(false);
          this.isPublishOpen.set(false);
          this.toaster.success('Site::ThemePublished', undefined, { messageLocalizationParams: [String(release.number)] });
          this.refreshTheme();
          this.refreshFiles();
          this.runCheck();
        },
        error: (response: ApiErrorResponse) => {
          this.isBusy.set(false);
          this.publishError.set(errorMessageOf(response));
          if (response.error?.error?.code === CHANGED_SINCE_CHECK) {
            // Show what is there now; the author publishes again from the fresh check.
            this.runCheck(fresh => this.publishCheck.set(fresh));
          }
        },
      });
  }

  // Export and import.

  /** Downloads the working copy as a zip package, or a release's when one is given. */
  exportTheme(release?: SiteReleaseDto): void {
    if (release && !release.id) {
      return;
    }

    // Named here as the server names it: a Content-Disposition header is not readable across origins.
    this.transferService.export(release?.id).subscribe(blob =>
      downloadBlob(blob, release ? `theme-release-${release.number}.zip` : 'theme-working-copy.zip'),
    );
  }

  openImport(): void {
    this.importFile.set(null);
    this.importReplace.set(false);
    this.importResult.set(undefined);
    this.isImportOpen.set(true);
  }

  onImportFileChange(event: Event): void {
    this.importFile.set((event.target as HTMLInputElement).files?.[0] ?? null);
    this.importResult.set(undefined);
  }

  /**
   * Writes the package into the working copy; nothing goes live. A package with any invalid entry is refused whole,
   * and ABP's error handler shows the problems. The result stays in the dialog, as the author reads what changed.
   */
  importTheme(): void {
    const file = this.importFile();
    if (!file || !this.canEdit || this.isBusy()) {
      return;
    }

    const body = new FormData();
    body.append('File', file, file.name);
    body.append('Replace', String(this.importReplace()));

    this.isBusy.set(true);
    this.transferService.import(body).subscribe({
      next: result => {
        this.isBusy.set(false);
        this.importResult.set(result);

        // Tabs of files the package touched would show what is no longer there; those with unsaved edits stay.
        const touched = new Set([...result.added, ...result.modified, ...result.deleted]);
        this.openFiles.update(files => files.filter(open => !touched.has(open.path) || isDirty(open)));
        if (!this.openFiles().some(open => open.path === this.activePath())) {
          this.activePath.set(this.openFiles()[0]?.path);
        }

        this.refreshTheme();
        this.refreshFiles();
        this.runCheck();
      },
      error: () => this.isBusy.set(false),
    });
  }

  /** The files with syntax errors in an import result, with their errors; empty when there are none. */
  syntaxErrorsOf(result: SiteThemeImportResultDto): { path: string; errors: SiteThemeSyntaxErrorDto[] }[] {
    return Object.entries(result.syntaxErrors ?? {})
      .filter(([, errors]) => errors.length > 0)
      .map(([path, errors]) => ({ path, errors }));
  }

  // Releases.

  openReleases(): void {
    this.isReleasesOpen.set(true);
    this.loadReleases();
  }

  private loadReleases(): void {
    this.themeService.getReleases().subscribe(result => this.releases.set(result.items ?? []));
  }

  makeLive(release: SiteReleaseDto): void {
    this.confirmation
      .warn('Site::MakeReleaseLiveConfirm', 'AbpUi::AreYouSure', { messageLocalizationParams: [String(release.number)] })
      .subscribe(status => {
        if (status !== Confirmation.Status.confirm || !release.id) {
          return;
        }

        this.themeService.setLiveRelease(release.id).subscribe(() => {
          this.toaster.success('Site::ReleaseMadeLive', undefined, { messageLocalizationParams: [String(release.number)] });
          this.loadReleases();
          this.refreshTheme();
          this.refreshFiles();
          this.runCheck();
        });
      });
  }

  restore(release: SiteReleaseDto): void {
    this.confirmation
      .warn('Site::RestoreReleaseConfirm', 'AbpUi::AreYouSure', { messageLocalizationParams: [String(release.number)] })
      .subscribe(status => {
        if (status !== Confirmation.Status.confirm || !release.id) {
          return;
        }

        this.themeService.restoreRelease(release.id).subscribe(() => {
          // Every open file may have changed; open tabs would show what is no longer there.
          this.openFiles.set([]);
          this.activePath.set(undefined);
          this.isReleasesOpen.set(false);
          this.toaster.success('Site::ReleaseRestored', undefined, { messageLocalizationParams: [String(release.number)] });
          this.refreshTheme();
          this.refreshFiles();
          this.runCheck();
        });
      });
  }

  // Default layout.

  /**
   * Makes the file in front the default layout, from its own toolbar: it is offered only for a file the check lists as
   * a layout, and the server judges again, from what is saved - what it writes and calls, through what it includes.
   */
  setAsDefaultLayout(): void {
    const path = this.activePath();
    if (!path || this.isBusy() || this.isDefaultLayoutInFront()) {
      return;
    }

    this.isBusy.set(true);
    this.themeService
      .update({ defaultLayout: path, concurrencyStamp: this.theme()?.concurrencyStamp }, { skipHandleError: true })
      .subscribe({
        next: theme => {
          this.isBusy.set(false);
          this.theme.set(theme);
          this.toaster.success('Site::DefaultLayoutSet', undefined, { messageLocalizationParams: [path] });
          this.runCheck();
        },
        error: (response: ApiErrorResponse) => {
          this.isBusy.set(false);
          this.toaster.error(errorMessageOf(response) || 'AbpUi::DefaultErrorMessage');
        },
      });
  }

  languageOf(path: string) {
    return languageOf(path);
  }

  private refreshTheme(): void {
    this.themeService.get().subscribe(theme => this.theme.set(theme));
  }

  private refreshFiles(): void {
    this.themeService.getFiles().subscribe(files => this.files.set(files.items ?? []));
  }

  private replaceOpenFile(path: string, file: ThemeOpenFile): void {
    this.openFiles.update(files => files.map(f => (f.path === path ? { ...file, pinned: file.pinned || f.pinned } : f)));
  }

  private bodyOf(path: string): string | undefined {
    return this.openFiles().find(file => file.path === path)?.body;
  }

  private expandTo(path: string): void {
    const folders = foldersOf(path);
    if (folders.some(folder => this.collapsed().has(folder))) {
      const next = new Set(this.collapsed());
      folders.forEach(folder => next.delete(folder));
      this.collapsed.set(next);
    }
  }
}

/** Saves a blob to disk through a link click; the object address is released once the click has been handled. */
function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url));
}

function isDirty(file: ThemeOpenFile): boolean {
  return file.body !== file.savedBody;
}

function toOpenFile(file: SiteThemeFileDto): ThemeOpenFile {
  return {
    path: file.path ?? '',
    isAsset: file.isAsset,
    body: file.body ?? '',
    savedBody: file.body ?? '',
    pinned: false,
    concurrencyStamp: file.concurrencyStamp,
    syntaxErrors: file.syntaxErrors ?? [],
  };
}

/** Who uses a file, in words: `Events (include), about (page)`; empty when nothing does. */
export function describeUsages(
  usages: SiteThemeFileUsagesDto,
  kindName: (kind: SiteThemeUsageKindDto) => string,
): string {
  return (usages.usages ?? [])
    .map(usage => `${usage.pageName ?? usage.sourcePath ?? ''} (${kindName(usage.kind)})`.trim())
    .join(', ');
}
