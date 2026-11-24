import { CoreModule, ListResultDto, PermissionDirective, PermissionService } from '@abp/ng.core';
import {
  Confirmation,
  ConfirmationService,
  ThemeSharedModule,
  ToasterService,
} from '@abp/ng.theme.shared';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import type { TreeStatus } from '@swimlane/ngx-datatable';
import {
  Component,
  inject,
  signal,
  ChangeDetectionStrategy,
} from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import type { GetPageListInput } from '../../proxy/dignite/site/admin/pages/models';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { SiteThemeAdminService } from '../../proxy/dignite/site/admin/themes/site-theme-admin.service';
import { SiteThemeFileStatus } from '../../proxy/dignite/site/themes/site-theme-file-status.enum';
import { eSitePolicyNames } from '../../enums/policy-names';
import { SITE_CONSTS } from '../../site-consts';
import { PageTreeSelectComponent } from '../contents/page-tree-select.component';

const PAGE_CONSTS = SITE_CONSTS.page;

/** A page, plus the expand/collapse state `ngx-datatable`'s tree mode keeps on each row. */
type PageRow = PageDto & { treeStatus?: TreeStatus };

@Component({
  selector: 'site-pages',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pages.component.html',
  imports: [
    CoreModule,
    ThemeSharedModule,
    ReactiveFormsModule,
    NgxDatatableModule,
    PageTreeSelectComponent,
    PermissionDirective,
    RouterLink,
  ],
  styles: `
    /* ngx-datatable always renders its own .datatable-tree-button for every row in the tree column,
       with no chrome reset (bare native <button>) and no way to suppress it for a childless row -
       [treeToggleTemplate] is documented to override it but does not take effect in this library
       version (tried both content-projection via ngx-datatable-tree-toggle and a direct
       [treeToggleTemplate] binding; neither replaced the default). Hidden here instead, with our own
       toggle built into the column's ngx-datatable-cell-template - see pages.component.html - which
       only renders one when hasChildren() says there is something to expand. */
    :host ::ng-deep .datatable-tree-button {
      display: none;
    }

    .page-tree-toggle {
      border: none;
      background: transparent;
      padding: 0;
      width: 16px;
      cursor: pointer;
      color: inherit;
    }
  `,
})
export class PagesComponent {
  private readonly pageService = inject(PageAdminService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);
  private readonly permission = inject(PermissionService);
  private readonly themeService = inject(SiteThemeAdminService);

  readonly policies = eSitePolicyNames;

  /**
   * The whole (filtered) result set, unpaged - the datatable paginates it client-side.
   *
   * State that changes in an HTTP callback (this, and the modal flags below) is a signal, not a plain
   * field: the host layout's ancestors (`abp-router-outlet`, the LeptonX layouts) are OnPush, and a
   * plain-field write in a response callback never marks them for check, so the list would stay empty
   * until some unrelated event happened to.
   */
  readonly data = signal<ListResultDto<PageRow>>({ items: [] });
  filters = {} as GetPageListInput;

  readonly isModalOpen = signal(false);
  readonly isModalBusy = signal(false);
  /** The theme's template paths, suggested under the Template input. */
  readonly templateOptions = signal<string[]>([]);
  editingPage?: PageDto;
  form?: FormGroup;

  constructor() {
    this.applyFilters();
  }

  /** Whether the tree-toggle template should render a toggle for this row at all. */
  hasChildren(row: PageRow): boolean {
    return this.data().items.some(page => page.parentId === row.id);
  }

  applyFilters(): void {
    this.pageService.getList(this.filters).subscribe(result => {
      // A page's parentId can point at a page the current filter excludes (isActive/filter are
      // backend params, so a match can survive while its parent does not). Rather than let such a
      // row vanish from the tree, it renders at the root - normalized here once so every reader of
      // `data().items` (the datatable's tree grouping, the tree-select's candidate list) sees a
      // consistent, dangling-free parentId.
      const ids = new Set(result.items.map(page => page.id));

      this.data.set({
        items: result.items.map(page => ({
          ...page,
          parentId: page.parentId && ids.has(page.parentId) ? page.parentId : null,
          treeStatus: 'expanded' as TreeStatus,
        })),
      });
    });
  }

  clearFilters(): void {
    this.filters = {} as GetPageListInput;
    this.applyFilters();
  }

  /** Toggles one row's branch. `ngx-datatable` reads `treeStatus` off the row it is given back. */
  onTreeAction(event: { row: PageRow }): void {
    event.row.treeStatus = event.row.treeStatus === 'collapsed' ? 'expanded' : 'collapsed';
    this.data.set({ items: [...this.data().items] });
  }

  openCreate(): void {
    this.editingPage = undefined;
    this.form = this.buildForm();
    this.loadTemplateOptions();
    this.isModalOpen.set(true);
  }

  openEdit(page: PageDto): void {
    this.editingPage = page;
    this.form = this.buildForm(page);
    this.loadTemplateOptions();
    this.isModalOpen.set(true);
  }

  /**
   * The theme's templates, offered under the Template input. A suggestion list, not a choice: a page can name a
   * Razor view instead. Skipped without the Themes permission, and quietly empty when the theme cannot be read.
   */
  private loadTemplateOptions(): void {
    if (!this.permission.getGrantedPolicy(eSitePolicyNames.Themes)) {
      return;
    }

    this.themeService.getFiles({ skipHandleError: true }).subscribe({
      next: files =>
        this.templateOptions.set(
          (files.items ?? [])
            .filter(file => !file.isAsset && file.status !== SiteThemeFileStatus.Deleted)
            .map(file => file.path ?? ''),
        ),
      error: () => this.templateOptions.set([]),
    });
  }

  closeModal(): void {
    this.isModalOpen.set(false);
    this.form = undefined;
    this.editingPage = undefined;
  }

  save(): void {
    if (!this.form || this.form.invalid || this.isModalBusy()) {
      return;
    }

    this.isModalBusy.set(true);
    const value = this.form.getRawValue();
    const isCreating = !this.editingPage;

    const request$ = this.editingPage
      ? this.pageService.update(this.editingPage.id!, value)
      : this.pageService.create(value);

    request$.subscribe({
      next: page => {
        this.isModalBusy.set(false);
        this.closeModal();
        this.toaster.success('AbpUi::SavedSuccessfully');

        // A fresh page always has exactly one auto-created content type
        // (PageAdminAppService.CreateDefaultContentTypeAsync) still waiting for its fields - go straight
        // there instead of leaving the user to find the sitemap icon themselves.
        if (isCreating && this.permission.getGrantedPolicy(this.policies.ContentTypes)) {
          this.router.navigate(['/site/pages', page.id, 'content-types']);
          return;
        }

        this.applyFilters();
      },
      error: () => this.isModalBusy.set(false),
    });
  }

  delete(page: PageDto): void {
    // Not the generic AbpUi::ItemWillBeDeletedMessage: deleting a page also deletes its content types and
    // every content of those types, and this confirmation is the only place a person is told.
    this.confirmation
      .warn('Site::DeletePageConfirmMessage', 'AbpUi::AreYouSure', {
        messageLocalizationParams: [page.displayName ?? ''],
      })
      .subscribe((status: Confirmation.Status) => {
        if (status === Confirmation.Status.confirm) {
          this.pageService.delete(page.id!).subscribe(() => {
            this.applyFilters();
            this.toaster.success('AbpUi::DeletedSuccessfully');
          });
        }
      });
  }

  private parentCandidatesCache?: {
    items: PageRow[];
    editingId: string | undefined;
    pages: PageDto[];
  };

  /**
   * The "Parent page" picker's candidates: every page except the one being edited and its own
   * subtree - offering either as a choice would only trade a clean rejection in the picker for a
   * `PageParentCycleException` from the server.
   *
   * Cached against a cheap key, not recomputed every read: `site-page-tree-select` rebuilds its tree
   * whenever it is handed a new array, which a plain filter here would do on every change detection.
   */
  get parentCandidates(): PageDto[] {
    const editingId = this.editingPage?.id;
    const items = this.data().items;

    if (
      this.parentCandidatesCache?.items !== items ||
      this.parentCandidatesCache?.editingId !== editingId
    ) {
      const excluded = editingId ? this.subtreeIds(editingId) : new Set<string>();
      this.parentCandidatesCache = {
        items,
        editingId,
        pages: items.filter(page => !excluded.has(page.id!)),
      };
    }

    return this.parentCandidatesCache.pages;
  }

  /** `rootId` and everything beneath it, found by repeatedly sweeping for newly-matched children. */
  private subtreeIds(rootId: string): Set<string> {
    const ids = new Set<string>([rootId]);

    for (let grew = true; grew; ) {
      grew = false;
      for (const page of this.data().items) {
        if (page.parentId && ids.has(page.parentId) && !ids.has(page.id!)) {
          ids.add(page.id!);
          grew = true;
        }
      }
    }

    return ids;
  }

  private buildForm(page?: PageDto): FormGroup {
    return this.fb.group({
      name: [
        page?.name ?? '',
        [Validators.required, Validators.maxLength(PAGE_CONSTS.maxNameLength), Validators.pattern(SITE_CONSTS.namePattern)],
      ],
      displayName: [
        page?.displayName ?? '',
        [Validators.required, Validators.maxLength(PAGE_CONSTS.maxDisplayNameLength)],
      ],
      route: [
        page?.route ?? '',
        [Validators.required, Validators.maxLength(PAGE_CONSTS.maxRouteLength), Validators.pattern(PAGE_CONSTS.routePattern)],
      ],
      template: [
        page?.template ?? '',
        [Validators.required, Validators.maxLength(PAGE_CONSTS.maxTemplateLength), Validators.pattern(PAGE_CONSTS.templatePattern)],
      ],
      parentId: [page?.parentId ?? null],
      isActive: [page?.isActive ?? true],
    });
  }
}
