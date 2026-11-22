import { CoreModule } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ThemeSharedModule } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, model, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { SiteBlobAccess, SiteBlobDirectory, SiteBlobDirectoryService } from '../services/site-blob-management.service';

/** The key the top of the container has among the lists of children: the parent of the first level. */
const ROOT = '';

interface TreeRow {
  /** `null` is the top of the container, shown as "all files". */
  directory: SiteBlobDirectory | null;
  depth: number;
}

/**
 * The directories of one container, a level at a time as they are opened, with the top of the container as the first
 * row. A directory is chosen by clicking it; the toolbar creates a directory inside the one chosen, renames it and deletes
 * it (an empty one) - each only where `access` says the user may: Blob Management authorizes them by the grants on the
 * container and refuses the rest.
 *
 * State is signals throughout: the layout shell is OnPush, and a value written in a response callback would not repaint.
 */
@Component({
  selector: 'site-directory-tree',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CoreModule, ThemeSharedModule],
  template: `
    <div class="btn-group btn-group-sm mb-2" role="group">
      <button
        type="button"
        class="btn btn-outline-secondary"
        [disabled]="!access().createDirectories"
        [attr.title]="'Site::Files:NewDirectory' | abpLocalization"
        (click)="openCreate()"
      >
        <i class="fa fa-folder-plus" aria-hidden="true"></i>
      </button>
      <button
        type="button"
        class="btn btn-outline-secondary"
        [disabled]="!selectedDirectory() || !access().updateDirectories"
        [attr.title]="'Site::Files:RenameDirectory' | abpLocalization"
        (click)="openRename()"
      >
        <i class="fa fa-pencil" aria-hidden="true"></i>
      </button>
      <button
        type="button"
        class="btn btn-outline-danger"
        [disabled]="!selectedDirectory() || !access().deleteDirectories"
        [attr.title]="'Site::Files:DeleteDirectory' | abpLocalization"
        (click)="remove()"
      >
        <i class="fa fa-trash" aria-hidden="true"></i>
      </button>
    </div>

    <ul class="list-unstyled mb-0 site-directory-tree">
      @for (row of rows(); track row.directory?.id ?? 'root') {
        <li>
          <div
            class="d-flex align-items-center rounded px-1 py-1"
            [class.bg-primary-subtle]="isSelected(row)"
            [style.padding-left.rem]="row.depth * 1 + 0.25"
          >
            @if (row.directory?.hasChildren) {
              <button
                type="button"
                class="btn btn-sm btn-link p-0 me-1 text-muted"
                [attr.aria-label]="isExpanded(row) ? ('Site::Files:Collapse' | abpLocalization) : ('Site::Files:Expand' | abpLocalization)"
                (click)="toggle(row.directory!)"
              >
                <i class="fa" [class.fa-chevron-down]="isExpanded(row)" [class.fa-chevron-right]="!isExpanded(row)" aria-hidden="true"></i>
              </button>
            } @else {
              <span class="d-inline-block me-1" style="width: 0.9rem"></span>
            }
            <button type="button" class="btn btn-link p-0 text-start text-decoration-none flex-grow-1 text-break" (click)="select(row)">
              <i class="fa me-1" [class.fa-folder-o]="row.directory" [class.fa-home]="!row.directory" aria-hidden="true"></i>
              @if (row.directory) {
                {{ row.directory.name }}
              } @else {
                {{ 'Site::Files:AllFiles' | abpLocalization }}
              }
            </button>
          </div>
        </li>
      }
    </ul>

    <abp-modal [(visible)]="nameDialogOpen" [busy]="busy()" [options]="{ size: 'md' }">
      <ng-template #abpHeader>
        <h5>
          {{ (renaming() ? 'Site::Files:RenameDirectory' : 'Site::Files:NewDirectory') | abpLocalization }}
        </h5>
      </ng-template>
      <ng-template #abpBody>
        <label class="form-label" for="siteDirectoryName">{{ 'Site::Files:DirectoryName' | abpLocalization }}</label>
        <input
          id="siteDirectoryName"
          type="text"
          class="form-control"
          maxlength="256"
          [value]="name()"
          (input)="name.set($any($event.target).value)"
          (keydown.enter)="save(); $event.preventDefault()"
        />
      </ng-template>
      <ng-template #abpFooter>
        <button type="button" class="btn btn-secondary" abpClose>{{ 'AbpUi::Cancel' | abpLocalization }}</button>
        <button type="button" class="btn btn-primary" [disabled]="busy() || !name().trim()" (click)="save()">
          <i class="fa fa-check me-1" aria-hidden="true"></i>{{ 'AbpUi::Save' | abpLocalization }}
        </button>
      </ng-template>
    </abp-modal>
  `,
})
export class SiteDirectoryTreeComponent {
  private readonly directories = inject(SiteBlobDirectoryService);
  private readonly confirmation = inject(ConfirmationService);

  /** The container whose directories are shown. */
  readonly containerName = input.required<string>();

  /** What the user may do with the container's directories; each button needs its own. */
  readonly access = input<SiteBlobAccess>({ view: false, createDirectories: false, updateDirectories: false, deleteDirectories: false });

  /** The directory chosen; `null` is the top of the container. */
  readonly selected = model<string | null>(null);

  private readonly children = signal<ReadonlyMap<string, readonly SiteBlobDirectory[]>>(new Map());
  private readonly expanded = signal<ReadonlySet<string>>(new Set());

  protected readonly nameDialogOpen = signal(false);
  protected readonly name = signal('');
  protected readonly renaming = signal(false);
  protected readonly busy = signal(false);

  protected readonly rows = computed<TreeRow[]>(() => {
    const result: TreeRow[] = [{ directory: null, depth: 0 }];
    const expanded = this.expanded();
    const children = this.children();
    const walk = (parentKey: string, depth: number) => {
      for (const directory of children.get(parentKey) ?? []) {
        result.push({ directory, depth });
        if (expanded.has(directory.id)) {
          walk(directory.id, depth + 1);
        }
      }
    };
    walk(ROOT, 1);
    return result;
  });

  protected readonly selectedDirectory = computed(() => {
    const id = this.selected();
    if (!id) {
      return null;
    }

    for (const list of this.children().values()) {
      const found = list.find(directory => directory.id === id);
      if (found) {
        return found;
      }
    }

    return null;
  });

  constructor() {
    // A different container is a different tree.
    effect(() => {
      this.containerName();
      this.children.set(new Map());
      this.expanded.set(new Set());
      this.load(ROOT);
    });
  }

  protected isSelected(row: TreeRow): boolean {
    return (row.directory?.id ?? null) === this.selected();
  }

  protected isExpanded(row: TreeRow): boolean {
    return !!row.directory && this.expanded().has(row.directory.id);
  }

  protected select(row: TreeRow): void {
    this.selected.set(row.directory?.id ?? null);
  }

  protected toggle(directory: SiteBlobDirectory): void {
    const expanded = new Set(this.expanded());
    if (expanded.has(directory.id)) {
      expanded.delete(directory.id);
    } else {
      expanded.add(directory.id);
      if (!this.children().has(directory.id)) {
        this.load(directory.id);
      }
    }
    this.expanded.set(expanded);
  }

  protected openCreate(): void {
    this.renaming.set(false);
    this.name.set('');
    this.nameDialogOpen.set(true);
  }

  protected openRename(): void {
    const directory = this.selectedDirectory();
    if (!directory) {
      return;
    }

    this.renaming.set(true);
    this.name.set(directory.name);
    this.nameDialogOpen.set(true);
  }

  protected save(): void {
    const name = this.name().trim();
    if (!name || this.busy()) {
      return;
    }

    const directory = this.selectedDirectory();
    this.busy.set(true);

    if (this.renaming() && directory) {
      this.directories
        .rename(directory.id, name)
        .pipe(finalize(() => this.busy.set(false)))
        .subscribe(() => {
          this.nameDialogOpen.set(false);
          this.load(directory.parentId ?? ROOT);
        });
      return;
    }

    const parentId = this.selected();
    this.directories
      .create(this.containerName(), name, parentId)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe(created => {
        this.nameDialogOpen.set(false);
        if (parentId) {
          this.expanded.set(new Set(this.expanded()).add(parentId));
        }
        this.load(parentId ?? ROOT);
        this.selected.set(created.id);
      });
  }

  protected remove(): void {
    const directory = this.selectedDirectory();
    if (!directory) {
      return;
    }

    this.confirmation
      .warn('Site::Files:DeleteDirectoryConfirm', 'Site::Files:DeleteDirectory', {
        messageLocalizationParams: [directory.name],
      })
      .subscribe(status => {
        if (status !== Confirmation.Status.confirm) {
          return;
        }

        this.directories.delete(directory.id).subscribe(() => {
          this.selected.set(directory.parentId ?? null);
          this.load(directory.parentId ?? ROOT);
        });
      });
  }

  /** Reads one level again - the top of the container for `ROOT` - and tells its parent row whether it still has children. */
  private load(parentKey: string): void {
    this.directories.getList(this.containerName(), parentKey || null).subscribe(result => {
      const items = result.items ?? [];
      const next = new Map(this.children());
      next.set(parentKey, items);

      if (parentKey !== ROOT) {
        for (const [key, list] of next) {
          if (list.some(directory => directory.id === parentKey)) {
            next.set(
              key,
              list.map(directory => (directory.id === parentKey ? { ...directory, hasChildren: items.length > 0 } : directory)),
            );
          }
        }
      }

      this.children.set(next);
    });
  }
}
