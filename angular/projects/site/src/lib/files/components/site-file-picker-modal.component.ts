import { CoreModule } from '@abp/ng.core';
import { ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  model,
  output,
  signal,
  untracked,
} from '@angular/core';
import { catchError, concatMap, EMPTY, finalize, from, tap } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import type { SiteFileDto } from '../../proxy/dignite/site/admin/files/models';
import { FormatFileSizePipe } from '../pipes/format-file-size.pipe';
import { SiteBlobAccess, SiteBlobContainer, SiteBlobContainerService } from '../services/site-blob-management.service';
import { refuseFile } from '../services/site-file-limits';
import { SiteFileUploadService } from '../services/site-file-upload.service';
import { sizedFileUrl } from '../services/site-file-url';
import { SiteFileUrlService } from '../services/site-file-url.service';
import { SiteStoredFile, toStoredFile } from '../services/site-stored-file';
import { SiteUploadCleanup } from '../services/site-upload-cleanup';
import { SiteDirectoryTreeComponent } from './site-directory-tree.component';

const NO_ACCESS: SiteBlobAccess = { view: false, createDirectories: false, updateDirectories: false, deleteDirectories: false };

/**
 * The window that lets a user pick files from those already uploaded to a container: the files of the directory chosen,
 * a page at a time, searchable by name, with a way to upload another one into that directory. When the container allows
 * directories and the user may view them (`access.view` of Blob Management's container), the directory tree stands beside
 * the list; without that right the list shows the top of the container only.
 *
 * The files come from Site's administration API - Site's own permission for the container - and the directories and
 * the container's limits from Blob Management, with the user's token (`SiteBlobDirectoryService`). A file uploaded here
 * is reported to the page's `SiteUploadCleanup`, as `site-file-input` reports its own uploads; one only picked never is, so
 * saving a form never deletes a file another content may use.
 */
@Component({
  selector: 'site-file-picker-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CoreModule, ThemeSharedModule, FormatFileSizePipe, SiteDirectoryTreeComponent],
  template: `
    <abp-modal [(visible)]="visible" [busy]="uploading()" [options]="{ size: 'xl', scrollable: true }">
      <ng-template #abpHeader>
        <h5>{{ 'Site::Files:Library' | abpLocalization }} <small class="text-muted">{{ containerName() }}</small></h5>
      </ng-template>

      <ng-template #abpBody>
        <div class="row g-3">
          @if (showTree()) {
            <div class="col-md-4 col-lg-3">
              <site-directory-tree
                [containerName]="containerName()"
                [access]="access()"
                [selected]="directoryId()"
                (selectedChange)="chooseDirectory($event)"
              />
            </div>
          }

          <div [class]="showTree() ? 'col-md-8 col-lg-9' : 'col-12'">
            <div class="d-flex flex-wrap gap-2 mb-2">
              <input
                type="search"
                class="form-control form-control-sm flex-grow-1"
                style="max-width: 20rem"
                [placeholder]="'Site::Files:Search' | abpLocalization"
                [value]="filter()"
                (input)="searchFor($any($event.target).value)"
              />
              <input #picker type="file" class="d-none" [attr.accept]="accept()" [multiple]="multiple()" (change)="upload($event)" />
              <button type="button" class="btn btn-sm btn-outline-primary ms-auto" [disabled]="uploading()" (click)="picker.click()">
                @if (uploading()) {
                  <span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>
                } @else {
                  <i class="fa fa-upload me-1" aria-hidden="true"></i>
                }
                {{ 'Site::Files:Upload' | abpLocalization }}
              </button>
            </div>

            @if (!showTree() && container()?.allowDirectories) {
              <p class="text-muted small mb-2">{{ 'Site::Files:NoDirectoryAccess' | abpLocalization }}</p>
            }

            <div class="table-responsive">
              <table class="table table-hover table-sm align-middle mb-0">
                <tbody>
                  @for (file of items(); track file.id) {
                    <tr class="site-file-row" role="button" [class.table-active]="isSelected(file)" (click)="toggle(file)">
                      <td style="width: 2rem">
                        <input
                          class="form-check-input"
                          [type]="multiple() ? 'checkbox' : 'radio'"
                          [checked]="isSelected(file)"
                          tabindex="-1"
                          (click)="$event.stopPropagation(); toggle(file)"
                        />
                      </td>
                      <td style="width: 3.5rem">
                        @if (isImage(file)) {
                          <img [src]="thumbnail(file)" [alt]="file.name" class="rounded" style="width: 3rem; height: 3rem; object-fit: cover" />
                        } @else {
                          <i class="fa fa-file-o fs-3 text-muted" aria-hidden="true"></i>
                        }
                      </td>
                      <td class="text-break">{{ file.name }}</td>
                      <td class="text-nowrap text-muted">{{ file.size | formatFileSize }}</td>
                      <td class="text-nowrap text-muted d-none d-md-table-cell">{{ file.creationTime | date: 'yyyy-MM-dd HH:mm' }}</td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="5" class="text-center text-muted py-4">
                        @if (loading()) {
                          <span class="spinner-border spinner-border-sm" aria-hidden="true"></span>
                        } @else {
                          {{ 'Site::Files:NoFiles' | abpLocalization }}
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <div class="d-flex align-items-center justify-content-between mt-2">
              <small class="text-muted">{{ rangeText() }}</small>
              <div class="btn-group btn-group-sm">
                <button type="button" class="btn btn-outline-secondary" [disabled]="skip() === 0 || loading()" (click)="page(-1)">
                  <i class="fa fa-chevron-left" aria-hidden="true"></i>
                </button>
                <button type="button" class="btn btn-outline-secondary" [disabled]="skip() + pageSize >= total() || loading()" (click)="page(1)">
                  <i class="fa fa-chevron-right" aria-hidden="true"></i>
                </button>
              </div>
            </div>
          </div>
        </div>
      </ng-template>

      <ng-template #abpFooter>
        <span class="me-auto text-muted small">
          {{ 'Site::Files:SelectedCount' | abpLocalization: selection().size.toString() }}
        </span>
        <button type="button" class="btn btn-secondary" abpClose>{{ 'AbpUi::Cancel' | abpLocalization }}</button>
        <button type="button" class="btn btn-primary" [disabled]="selection().size === 0 || uploading()" (click)="confirm()">
          <i class="fa fa-check me-1" aria-hidden="true"></i>{{ 'Site::Files:Choose' | abpLocalization }}
        </button>
      </ng-template>
    </abp-modal>
  `,
})
export class SiteFilePickerModalComponent {
  private readonly files = inject(FileAdminService);
  private readonly containers = inject(SiteBlobContainerService);
  private readonly uploads = inject(SiteFileUploadService);
  private readonly toaster = inject(ToasterService);
  private readonly cleanup = inject(SiteUploadCleanup, { optional: true });
  private readonly urls = inject(SiteFileUrlService);

  /** One of Site's file containers: `images`, `attachments` or `theme-assets`. */
  readonly containerName = input.required<string>();

  /** Whether several files may be chosen; otherwise the choice is one file. */
  readonly multiple = input(false);

  /** Whether the window is open. */
  readonly visible = model(false);

  /** The files chosen, when the user confirms. */
  readonly picked = output<SiteStoredFile[]>();

  protected readonly pageSize = 10;
  protected readonly container = signal<SiteBlobContainer | null>(null);
  protected readonly directoryId = signal<string | null>(null);
  protected readonly filter = signal('');
  protected readonly skip = signal(0);
  protected readonly total = signal(0);
  protected readonly items = signal<readonly SiteFileDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly uploading = signal(false);
  /** The files chosen so far, over every page and directory the user went through. */
  protected readonly selection = signal<ReadonlyMap<string, SiteStoredFile>>(new Map());

  protected readonly access = computed(() => this.container()?.access ?? NO_ACCESS);
  protected readonly showTree = computed(() => !!this.container()?.allowDirectories && this.access().view);

  /** The file input's `accept`: the container's allowed extensions; `null` - no attribute - for any. */
  protected readonly accept = computed(() => {
    const extensions = this.container()?.allowedFileExtensions ?? [];
    return extensions.length > 0 ? extensions.join(',') : null;
  });

  protected readonly rangeText = computed(() => {
    const total = this.total();
    if (total === 0) {
      return '';
    }

    return `${this.skip() + 1}-${Math.min(this.skip() + this.pageSize, total)} / ${total}`;
  });

  private searchTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    effect(() => {
      if (this.visible()) {
        untracked(() => this.open());
      }
    });
  }

  protected isSelected(file: SiteFileDto): boolean {
    return this.selection().has(file.id ?? '');
  }

  protected isImage(file: SiteFileDto): boolean {
    return (file.mimeType ?? '').startsWith('image/');
  }

  protected thumbnail(file: SiteFileDto): string {
    return this.urls.toDisplay(sizedFileUrl(file.url, 96, 96));
  }

  protected chooseDirectory(id: string | null): void {
    this.directoryId.set(id);
    this.skip.set(0);
    this.loadFiles();
  }

  protected searchFor(text: string): void {
    this.filter.set(text);
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.skip.set(0);
      this.loadFiles();
    }, 300);
  }

  protected page(direction: 1 | -1): void {
    this.skip.set(Math.max(0, this.skip() + direction * this.pageSize));
    this.loadFiles();
  }

  protected toggle(file: SiteFileDto): void {
    const id = file.id ?? '';
    const next = new Map(this.multiple() ? this.selection() : []);
    if (this.multiple() && next.has(id)) {
      next.delete(id);
    } else {
      next.set(id, toStoredFile(file));
    }
    this.selection.set(next);
  }

  protected confirm(): void {
    this.picked.emit([...this.selection().values()]);
    this.visible.set(false);
  }

  /**
   * Uploads the files chosen, one after another, into the directory shown, leaving out any the container would refuse.
   * Each one uploaded is chosen as soon as it is kept; the list is read again at the end.
   */
  protected upload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const chosen = Array.from(input.files ?? []);
    input.value = '';

    const files = chosen.filter(file => {
      const refusal = refuseFile(file, this.container());
      if (refusal) {
        this.toaster.error(refusal.key, '', { messageLocalizationParams: refusal.params });
      }
      return !refusal;
    });
    if (files.length === 0) {
      return;
    }

    // ABP's error handler has shown each failure already; the batch goes on and one toast counts them at the end.
    let failed = 0;
    this.uploading.set(true);
    from(files)
      .pipe(
        concatMap(file =>
          this.uploads.upload(file, this.containerName(), this.directoryId()).pipe(
            tap(uploaded => {
              this.cleanup?.uploaded(uploaded);
              const next = new Map(this.multiple() ? this.selection() : []);
              next.set(uploaded.id, uploaded);
              this.selection.set(next);
            }),
            catchError(() => {
              failed++;
              return EMPTY;
            }),
          ),
        ),
        finalize(() => {
          this.uploading.set(false);
          if (failed > 0) {
            this.toaster.error('Site::Files:UploadFailed', '', { messageLocalizationParams: [String(failed), String(files.length)] });
          }
          this.skip.set(0);
          this.loadFiles();
        }),
      )
      .subscribe();
  }

  private open(): void {
    this.selection.set(new Map());
    this.directoryId.set(null);
    this.filter.set('');
    this.skip.set(0);
    this.items.set([]);
    this.total.set(0);
    this.container.set(null);

    // Without the container's description the list still works - at the top of the container, with Blob Management's own
    // checks on upload - so a failure here is not shown.
    this.containers
      .get(this.containerName(), { skipHandleError: true })
      .pipe(catchError(() => EMPTY))
      .subscribe(container => this.container.set(container));

    this.loadFiles();
  }

  private loadFiles(): void {
    this.loading.set(true);
    this.files
      .getList({
        containerName: this.containerName(),
        directoryId: this.directoryId() ?? undefined,
        filter: this.filter().trim() || undefined,
        sorting: 'CreationTime desc',
        skipCount: this.skip(),
        maxResultCount: this.pageSize,
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe(result => {
        this.items.set(result.items ?? []);
        this.total.set(result.totalCount ?? 0);
      });
  }
}
