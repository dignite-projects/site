import { TreeAdapter, TreeModule } from '@abp/ng.components/tree';
import type { BaseNode, TreeNode } from '@abp/ng.components/tree';
import { CoreModule } from '@abp/ng.core';
import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  Output,
  inject,
} from '@angular/core';
import type { PageDto } from '../../proxy/dignite/site/pages/models';

/** `TreeAdapter` wants a non-optional `id`/`parentId`, which the generated `PageDto` leaves optional. */
type PageNode = PageDto & BaseNode;

/**
 * Picks one page, laid out as the page hierarchy rather than a flat list - a child page's name alone
 * rarely says which section it sits under.
 *
 * Built like flex-fields' `ff-tree-search` - a `.form-select`-styled toggle over an `abp-tree` panel -
 * because both render in the same content-list filter bar and should look and behave alike. That
 * component's node renderer (`ff-tree-picker-nodes`) is not exported, hence `abp-tree` directly.
 */
@Component({
  selector: 'site-page-tree-select',
  imports: [CoreModule, TreeModule],
  template: `
    <div class="page-tree-select">
      <button
        type="button"
        class="form-select text-start page-tree-select-toggle"
        [class.text-muted]="!selectedNode"
        aria-haspopup="tree"
        [attr.aria-expanded]="isOpen"
        (click)="toggle()"
      >
        {{ selectedNode?.title ?? '--' }}
      </button>
      @if (selectedNode) {
        <button
          type="button"
          class="btn btn-link page-tree-select-clear"
          [title]="'AbpUi::Clear' | abpLocalization"
          (click)="clear($event)"
        >
          <i class="fa fa-times" aria-hidden="true"></i>
        </button>
      }

      @if (isOpen) {
        <div class="page-tree-select-menu">
          <abp-tree
            [nodes]="nodes"
            [(expandedKeys)]="expandedKeys"
            [selectedNode]="selectedNode"
            (selectedNodeChange)="select($event)"
          >
            <ng-template abpTreeNodeTemplate let-node>
              <span class="page-tree-select-node">{{ node.title }}</span>
            </ng-template>
            <ng-template abpTreeExpandedIconTemplate let-node>
              <i
                class="fa"
                [class.fa-minus]="node.isExpanded"
                [class.fa-plus]="!node.isExpanded"
                aria-hidden="true"
              ></i>
            </ng-template>
          </abp-tree>
        </div>
      }
    </div>
  `,
  // Mirrors ff-tree-search's and ff-tree-picker-nodes' styles, so this sits beside a Tree field's
  // filter without looking like a different control. The panel renders inside the host, not in an
  // overlay, so :host-scoped rules reach it.
  styles: `
    .page-tree-select {
      position: relative;
    }
    .page-tree-select-toggle {
      height: auto;
      min-height: calc(1.5em + 1.35rem + 2px);
      padding-right: 3.75rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .page-tree-select-clear {
      position: absolute;
      top: 50%;
      right: 2rem;
      display: inline-flex;
      align-items: center;
      height: 100%;
      padding: 0 0.375rem;
      line-height: 1;
      color: var(--bs-secondary-color, #6c757d);
      text-decoration: none;
      transform: translateY(-50%);
      z-index: 2;
    }
    .page-tree-select-menu {
      position: absolute;
      z-index: 1050;
      width: 100%;
      max-height: 18rem;
      margin-top: 0.25rem;
      padding: 0.375rem;
      overflow: auto;
      color: var(--bs-body-color);
      background-color: var(--lpx-content-bg, var(--bs-secondary-bg, #fff));
      border: 1px solid var(--bs-border-color, rgba(0, 0, 0, 0.12));
      border-radius: 0.375rem;
      box-shadow: 0 0.125rem 0.25rem #00000013;
    }
    .page-tree-select-node {
      display: inline-block;
      min-height: 1.75rem;
      padding: 0.125rem 0.375rem;
      border-radius: 0.25rem;
    }
    :host ::ng-deep .ant-tree {
      background: transparent !important;
    }
    :host ::ng-deep .ant-tree-node-content-wrapper,
    :host ::ng-deep .ant-tree-node-content-wrapper:hover {
      background-color: transparent !important;
    }
    :host ::ng-deep .ant-tree-node-content-wrapper > .selected .page-tree-select-node {
      background-color: var(--lpx-brand, var(--bs-primary));
      color: var(--bs-white, #fff);
    }
    :host ::ng-deep .ant-tree-node-content-wrapper:hover > div:not(.selected) .page-tree-select-node {
      background-color: var(--bs-secondary-bg, #f5f5f5);
    }
  `,
})
export class PageTreeSelectComponent {
  private readonly elementRef = inject(ElementRef);

  /** Every page, flat - the hierarchy is rebuilt from `parentId`. */
  @Input() set pages(pages: PageDto[]) {
    const ids = new Set(pages.map(page => page.id));
    // `TreeAdapter` silently drops a node whose parent is not in the list, taking its whole subtree
    // with it; such a page shows at the root instead.
    const list = pages.map(
      page =>
        ({
          ...page,
          id: page.id!,
          parentId: page.parentId && ids.has(page.parentId) ? page.parentId : null,
        }) as PageNode,
    );

    this.nodes = new TreeAdapter(list).getTree();
    this.nodesByKey = new Map();
    this.index(this.nodes);
    // Opens fully expanded, the same as the page form's parent picker (`nzDefaultExpandAll`).
    this.expandedKeys = [...this.nodesByKey.values()].filter(node => !node.isLeaf).map(node => node.key);
  }

  /** The selected page's id, or `null` for "any page". */
  @Input() value?: string | null;

  /** Emits only on an actual change, like a native `<select>`'s `change`. */
  @Output() readonly valueChange = new EventEmitter<string | null>();

  nodes: TreeNode<PageNode>[] = [];
  expandedKeys: string[] = [];
  isOpen = false;

  private nodesByKey = new Map<string, TreeNode<PageNode>>();

  /**
   * Passed to `abp-tree` as `selectedNode`, whose default `isNodeSelected` matches on `id` - which a
   * `TreeNode` carries as well as its entity. A stable reference from {@link nodesByKey}, so the
   * OnPush tree only re-renders when the selection really changes.
   */
  get selectedNode(): TreeNode<PageNode> | undefined {
    return this.value ? this.nodesByKey.get(this.value) : undefined;
  }

  toggle(): void {
    this.isOpen = !this.isOpen;
  }

  /** `abp-tree` emits the clicked node's entity, i.e. the page. */
  select(page: PageNode): void {
    this.isOpen = false;
    this.emit(page.id);
  }

  clear(event: Event): void {
    // Keeps the click from reaching the toggle underneath it.
    event.stopPropagation();
    this.emit(null);
  }

  @HostListener('document:click', ['$event'])
  closeOnOutsideClick(event: Event): void {
    if (this.isOpen && !event.composedPath().includes(this.elementRef.nativeElement)) {
      this.isOpen = false;
    }
  }

  @HostListener('keydown.escape')
  close(): void {
    this.isOpen = false;
  }

  private emit(pageId: string | null): void {
    if (pageId !== (this.value ?? null)) {
      this.valueChange.emit(pageId);
    }
  }

  private index(nodes: TreeNode<PageNode>[]): void {
    for (const node of nodes) {
      this.nodesByKey.set(node.key, node);
      this.index(node.children);
    }
  }
}
