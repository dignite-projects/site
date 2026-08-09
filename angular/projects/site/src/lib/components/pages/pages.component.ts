import { CoreModule, ListResultDto, PermissionDirective, PermissionService } from '@abp/ng.core';
import {
  Confirmation,
  ConfirmationService,
  ThemeSharedModule,
  ToasterService,
} from '@abp/ng.theme.shared';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import type { DragEventData, TreeStatus } from '@swimlane/ngx-datatable';
import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
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
  maxRouteLength: 256,
  maxTemplateLength: 256,
} as const;

/** A page, plus the expand/collapse state `ngx-datatable`'s tree mode keeps on each row. */
type PageRow = PageDto & { treeStatus?: TreeStatus };

/** Matches every `{name:FORMAT}` placeholder in a route, to check FORMAT - see {@link routeFormatValidator}. */
const FORMAT_SUFFIX = /\{[a-zA-Z][a-zA-Z0-9]*:([^}]+)\}/g;

/** What `FORMAT` may contain - mirrors `PageRoute.IsValid`'s server-side regex exactly. */
const ALLOWED_FORMAT_CHARACTERS = /^[a-zA-Z0-9_.-]+$/;

/**
 * Catches a `:FORMAT` suffix containing `/` before the round trip to the server does -
 * `PageRoute.IsValid` rejects it there for the same reason (总体设计 §3.3): `/` in a formatted value
 * would be indistinguishable from the `/` that separates one path segment from the next. Not a rule
 * about dates specifically - it runs the same way whatever the placeholder's name is.
 */
function routeFormatValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value as string | null;
    if (!value) {
      return null;
    }

    FORMAT_SUFFIX.lastIndex = 0;
    for (let match = FORMAT_SUFFIX.exec(value); match; match = FORMAT_SUFFIX.exec(value)) {
      if (!ALLOWED_FORMAT_CHARACTERS.test(match[1])) {
        return { invalidPlaceholderFormat: true };
      }
    }

    return null;
  };
}

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
    ::ng-deep .parent-picker-dropdown .ant-select-dropdown {
      background: var(--lpx-content-bg, #fff) !important;
    }
    ::ng-deep .parent-picker-dropdown .ant-tree-node-content-wrapper:hover {
      background-color: var(--bs-primary) !important;
      color: var(--bs-white) !important;
    }
    ::ng-deep .parent-picker-dropdown .ant-tree-node-selected {
      background-color: var(--lpx-brand) !important;
      color: var(--bs-white) !important;
    }
    ::ng-deep .parent-picker-dropdown .ant-select-tree {
      color: var(--bs-body-color);
      background-color: transparent;
    }
  `,
})
export class PagesComponent {
  private readonly pageService = inject(PageAdminService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
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

  /**
   * Gates `rowDraggable` on the list. `ngx-datatable` has no per-row drag switch - only this
   * table-wide one - so a page a caller cannot update can still be picked up; `onRowDragEvent`
   * would refuse the resulting `move` call anyway via the server's own authorization, but gating
   * the drag here avoids the pointless round trip.
   */
  get canDrag(): boolean {
    return this.permission.getGrantedPolicy(this.policies.PagesUpdate);
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
      // `data.items` (the datatable's tree grouping, the tree-select's candidate list, the drag
      // guards) sees a consistent, dangling-free parentId.
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

  /**
   * Interprets a drop as "the dragged page becomes the dropped-on page's last child" - the only
   * rule `rowDragEvents` needs, since the library hands back just `{ dragRow, dropRow }` and leaves
   * every interaction choice to the caller. Reordering among existing siblings, and promoting a
   * page back to the root, both go through the Order/"Parent page" fields in the edit form instead
   * - drag here only ever reparents.
   */
  onRowDragEvent(event: DragEventData): void {
    if (event.eventType !== 'drop' || !event.dropRow) {
      return;
    }

    const dragRow = event.dragRow as PageRow;
    const dropRow = event.dropRow as PageRow;

    if (dragRow.id === dropRow.id) {
      return;
    }

    if (dragRow.isHomePage) {
      this.toaster.warn('Site::PageMoveHomePageRejected');
      return;
    }

    if (this.isSelfOrDescendant(dropRow.id!, dragRow.id!)) {
      this.toaster.warn('Site::PageMoveCycleRejected');
      return;
    }

    const newSiblingCount = this.data.items.filter(page => page.parentId === dropRow.id).length;

    this.pageService.move(dragRow.id!, { parentId: dropRow.id!, order: newSiblingCount }).subscribe({
      // Refreshing on both outcomes matters, not just error: ngx-datatable has already mutated its
      // own internal row order optimistically for the drag gesture, so even a *successful* move
      // needs the reload to reconcile the client's guess with the server's actual renumbering.
      next: () => this.applyFilters(),
      error: () => this.applyFilters(),
    });
  }

  /** Whether `candidateId` is `ancestorId` itself, or sits anywhere beneath it in the tree. */
  private isSelfOrDescendant(candidateId: string, ancestorId: string): boolean {
    let current = this.data.items.find(page => page.id === candidateId);

    while (current) {
      if (current.id === ancestorId) {
        return true;
      }

      current = this.data.items.find(page => page.id === current!.parentId);
    }

    return false;
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

    const request$ = this.editingPage
      ? this.pageService.update(this.editingPage.id!, value)
      : this.pageService.create(value);

    request$.subscribe({
      next: () => {
        this.isModalBusy = false;
        this.closeModal();
        this.applyFilters();
        this.toaster.success('AbpUi::SavedSuccessfully');
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
        [Validators.required, Validators.maxLength(PAGE_CONSTS.maxNameLength)],
      ],
      displayName: [
        page?.displayName ?? '',
        [Validators.required, Validators.maxLength(PAGE_CONSTS.maxDisplayNameLength)],
      ],
      route: [
        page?.route ?? '',
        [Validators.required, Validators.maxLength(PAGE_CONSTS.maxRouteLength), routeFormatValidator()],
      ],
      template: [page?.template ?? '', [Validators.maxLength(PAGE_CONSTS.maxTemplateLength)]],
      isHomePage: [page?.isHomePage ?? false],
      order: [page?.order ?? 0],
      parentId: [page?.parentId ?? null],
      isActive: [page?.isActive ?? true],
    });
  }
}
