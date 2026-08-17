import { CoreModule, ListResultDto, PermissionDirective, PermissionService } from '@abp/ng.core';
import {
  Confirmation,
  ConfirmationService,
  ThemeSharedModule,
  ToasterService,
} from '@abp/ng.theme.shared';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import type { TreeStatus } from '@swimlane/ngx-datatable';
import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NzTreeSelectModule } from 'ng-zorro-antd/tree-select';
import type { NzTreeNodeOptions } from 'ng-zorro-antd/tree';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import type { GetPageListInput } from '../../proxy/dignite/site/admin/pages/models';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { eSitePolicyNames } from '../../enums/policy-names';

/** Max lengths mirror `PageConsts` (`src/Dignite.Site.Domain.Shared/Pages/PageConsts.cs`). */
const PAGE_CONSTS = {
  maxNameLength: 64,
  maxDisplayNameLength: 128,
  maxRouteLength: 512,
  maxTemplateLength: 256,
  maxContentTemplateLength: 256,
} as const;

/** Mirrors `IdentifierName.Pattern` (`src/Dignite.Site.Domain.Shared/IdentifierName.cs`). */
const NAME_PATTERN = /^[a-z0-9][a-z0-9_-]*$/;

/** Mirrors `PageConsts.RoutePattern`. */
const ROUTE_PATTERN = /^\S+$/;

/** Mirrors `PageConsts.TemplatePattern`. Empty matches too - `template` is optional. */
const TEMPLATE_PATTERN = /^(\/?[A-Za-z0-9_][A-Za-z0-9_/-]*)?$/;

/** A page, plus the expand/collapse state `ngx-datatable`'s tree mode keeps on each row. */
type PageRow = PageDto & { treeStatus?: TreeStatus };

@Component({
  selector: 'site-pages',
  templateUrl: './pages.component.html',
  imports: [
    CoreModule,
    ThemeSharedModule,
    ReactiveFormsModule,
    NgxDatatableModule,
    NzTreeSelectModule,
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

    /* Dresses nz-tree-select up as a Bootstrap .form-select, matching field-arrangement.component.ts's
       .field-picker-select for the same reason: ng-zorro's own look does not match the plain
       .form-control/.form-select styling of every other field in this form.

       Plain ::ng-deep, not :host ::ng-deep - this form lives inside abp-modal, which ng-bootstrap
       renders as an NgbModalWindow appended straight to <body>, outside <site-pages>'s own DOM
       subtree. A :host-scoped rule compiles to an attribute-selector prefix scoped to this
       component's own host element, which can only ever match a descendant of that element - never
       true here, so the rule silently never applied and the select fell back to ng-zorro's raw
       styling (a barely 8px-tall selector box, nothing like the surrounding .form-control fields).
       The .parent-picker-dropdown rules below already got this right; these four did not. */
    ::ng-deep .parent-picker-select {
      width: 100%;
    }
    ::ng-deep .parent-picker-select.ant-select {
      color: inherit !important;
      height:unset !important;
      font-size: unset !important;
      --bs-form-select-bg-img: unset !important;
    }
    ::ng-deep .parent-picker-select .ant-select-selector {
      background: transparent !important;
      border: none !important;
      padding: 0.475rem 1.25rem !important;
      box-shadow: none !important;
      height: auto !important;
    }
    ::ng-deep .parent-picker-select .ant-select-selection-placeholder {
      color: inherit !important;
    }
    ::ng-deep .parent-picker-select.form-select {
      padding: 0 !important;
    }
    /* Compound, not descendant - nz-tree-select builds one class STRING ("ant-select-dropdown
       ant-select-tree-dropdown parent-picker-dropdown") and binds it with [class] on a single <div>
       (see NzTreeSelectComponent's dropdownClassName/ngOnChanges in ng-zorro-antd-tree-select.mjs),
       unlike nz-select, which puts nzDropdownClassName on the ancestor .cdk-overlay-pane via
       [cdkConnectedOverlayPanelClass] (see field-arrangement.component.ts's identically-shaped rule,
       which is a real ancestor-descendant match). ".parent-picker-dropdown .ant-select-dropdown"
       (with a space) asks for .ant-select-dropdown *inside* .parent-picker-dropdown, but they're the
       same element here, so that never matched anything and this panel stayed on ant-design's own
       white default no matter what the nested rules below did. */
    ::ng-deep .parent-picker-dropdown.ant-select-dropdown {
      background: var(--lpx-content-bg, #fff) !important;
    }
    /* nz-tree's per-node classes drop the "select-" infix outside of a select's dropdown
       (NzTreeNodeComponent's host bindings key every tree class off selectMode - see
       ng-zorro-antd-tree.mjs) - nz-tree-select's own embedded tree always runs in select mode, so
       its nodes carry ant-select-tree-node-content-wrapper/-selected, never the plain
       ant-tree-node-* classes a standalone <nz-tree> would use. The un-prefixed selectors here
       matched nothing, silently leaving every node on ant-design's own default colors. */
    ::ng-deep .parent-picker-dropdown .ant-select-tree-node-content-wrapper:hover {
      background-color: var(--bs-primary) !important;
      color: var(--bs-white) !important;
    }
    ::ng-deep .parent-picker-dropdown .ant-select-tree-node-content-wrapper.ant-select-tree-node-selected {
      background-color: var(--lpx-brand) !important;
      color: var(--bs-white) !important;
    }
    /* !important, matching every other rule in this block - ant-design's own .ant-select-tree rule
       (background: #fff, color: rgba(0,0,0,.85)) carries the same one-class specificity as this
       override, and its stylesheet is injected lazily when nz-tree-select first opens, i.e. after
       this component's styles - a same-specificity tie that source order was resolving in its
       favor, keeping idle (non-hover, non-selected) nodes on ant-design's white/dark-gray default. */
    ::ng-deep .parent-picker-dropdown .ant-select-tree {
      color: var(--bs-body-color) !important;
      background-color: transparent !important;
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

  readonly policies = eSitePolicyNames;

  /** The whole (filtered) result set, unpaged - the datatable paginates it client-side. */
  data: ListResultDto<PageRow> = { items: [] };
  filters = {} as GetPageListInput;

  isModalOpen = false;
  isModalBusy = false;
  editingPage?: PageDto;
  form?: FormGroup;

  @ViewChild('submitButton') submitButton?: ElementRef<HTMLButtonElement>;

  constructor() {
    this.applyFilters();
  }

  /** Whether the tree-toggle template should render a toggle for this row at all. */
  hasChildren(row: PageRow): boolean {
    return this.data.items.some(page => page.parentId === row.id);
  }

  applyFilters(): void {
    this.pageService.getList(this.filters).subscribe(result => {
      // A page's parentId can point at a page the current filter excludes (isActive/filter are
      // backend params, so a match can survive while its parent does not). Rather than let such a
      // row vanish from the tree, it renders at the root - normalized here once so every reader of
      // `data.items` (the datatable's tree grouping, the tree-select's candidate list) sees a
      // consistent, dangling-free parentId.
      const ids = new Set(result.items.map(page => page.id));

      this.data = {
        items: result.items.map(page => ({
          ...page,
          parentId: page.parentId && ids.has(page.parentId) ? page.parentId : null,
          treeStatus: 'expanded' as TreeStatus,
        })),
      };
    });
  }

  clearFilters(): void {
    this.filters = {} as GetPageListInput;
    this.applyFilters();
  }

  /** Toggles one row's branch. `ngx-datatable` reads `treeStatus` off the row it is given back. */
  onTreeAction(event: { row: PageRow }): void {
    event.row.treeStatus = event.row.treeStatus === 'collapsed' ? 'expanded' : 'collapsed';
    this.data = { items: [...this.data.items] };
  }

  openCreate(): void {
    this.editingPage = undefined;
    this.form = this.buildForm();
    this.isModalOpen = true;
  }

  openEdit(page: PageDto): void {
    this.editingPage = page;
    this.form = this.buildForm(page);
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.form = undefined;
    this.editingPage = undefined;
  }

  save(): void {
    if (!this.form || this.form.invalid || this.isModalBusy) {
      return;
    }

    this.isModalBusy = true;
    const value = this.form.getRawValue();
    const isCreating = !this.editingPage;

    const request$ = this.editingPage
      ? this.pageService.update(this.editingPage.id!, value)
      : this.pageService.create(value);

    request$.subscribe({
      next: page => {
        this.isModalBusy = false;
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
      error: () => (this.isModalBusy = false),
    });
  }

  delete(page: PageDto): void {
    this.confirmation
      .warn('AbpUi::ItemWillBeDeletedMessage', 'AbpUi::AreYouSure', {
        messageLocalizationParams: [page.displayName ?? ''],
      })
      .subscribe((status: Confirmation.Status) => {
        if (status === Confirmation.Status.confirm) {
          this.pageService.delete(page.id!).subscribe(() => {
            this.applyFilters();
            this.toaster.success('AbpUi::SuccessfullyDeleted');
          });
        }
      });
  }

  private parentTreeNodesCache?: {
    items: PageRow[];
    editingId: string | undefined;
    nodes: NzTreeNodeOptions[];
  };

  /**
   * The "Parent page" picker's candidates: every page except the one being edited and its own
   * subtree - offering either as a choice would only trade a clean rejection in the picker for a
   * `PageParentCycleException` from the server.
   *
   * Cached against a cheap key, not recomputed every read, for the same reason
   * `field-arrangement.component.ts`'s `addableFieldOptions` is: `nz-tree-select` expects a stable
   * array reference, and a plain rebuild here would hand it a fresh one on every change-detection
   * cycle, including the ones firing while its dropdown is open.
   */
  get parentTreeNodes(): NzTreeNodeOptions[] {
    const editingId = this.editingPage?.id;

    if (
      this.parentTreeNodesCache?.items !== this.data.items ||
      this.parentTreeNodesCache?.editingId !== editingId
    ) {
      const excluded = editingId ? this.subtreeIds(editingId) : new Set<string>();
      this.parentTreeNodesCache = {
        items: this.data.items,
        editingId,
        nodes: this.buildTreeNodes(null, excluded),
      };
    }

    return this.parentTreeNodesCache.nodes;
  }

  /** `rootId` and everything beneath it, found by repeatedly sweeping for newly-matched children. */
  private subtreeIds(rootId: string): Set<string> {
    const ids = new Set<string>([rootId]);

    for (let grew = true; grew; ) {
      grew = false;
      for (const page of this.data.items) {
        if (page.parentId && ids.has(page.parentId) && !ids.has(page.id!)) {
          ids.add(page.id!);
          grew = true;
        }
      }
    }

    return ids;
  }

  private buildTreeNodes(parentId: string | null, excluded: Set<string>): NzTreeNodeOptions[] {
    return this.data.items
      .filter(page => (page.parentId ?? null) === parentId && !excluded.has(page.id!))
      .map(page => ({
        key: page.id!,
        title: page.displayName || page.name || '',
        children: this.buildTreeNodes(page.id!, excluded),
        isLeaf: !this.data.items.some(child => child.parentId === page.id),
      }));
  }

  private buildForm(page?: PageDto): FormGroup {
    return this.fb.group({
      name: [
        page?.name ?? '',
        [Validators.required, Validators.maxLength(PAGE_CONSTS.maxNameLength), Validators.pattern(NAME_PATTERN)],
      ],
      displayName: [
        page?.displayName ?? '',
        [Validators.required, Validators.maxLength(PAGE_CONSTS.maxDisplayNameLength)],
      ],
      route: [
        page?.route ?? '',
        [Validators.required, Validators.maxLength(PAGE_CONSTS.maxRouteLength), Validators.pattern(ROUTE_PATTERN)],
      ],
      template: [
        page?.template ?? '',
        [Validators.maxLength(PAGE_CONSTS.maxTemplateLength), Validators.pattern(TEMPLATE_PATTERN)],
      ],
      contentTemplate: [
        page?.contentTemplate ?? '',
        [Validators.maxLength(PAGE_CONSTS.maxContentTemplateLength), Validators.pattern(TEMPLATE_PATTERN)],
      ],
      parentId: [page?.parentId ?? null],
      isActive: [page?.isActive ?? true],
    });
  }
}
