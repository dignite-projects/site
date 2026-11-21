import { CoreModule } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, computed, effect, forwardRef, inject, input, model, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { catchError, concatMap, EMPTY, finalize, from, tap } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import type { SiteFileContainerDto } from '../../proxy/dignite/site/admin/files/models';
import { FormatFileSizePipe } from '../pipes/format-file-size.pipe';
import { SiteFileUploadService } from '../services/site-file-upload.service';
import { SiteFileUrlService } from '../services/site-file-url.service';
import { SiteStoredFile } from '../services/site-stored-file';
import { SiteUploadCleanup } from '../services/site-upload-cleanup';

/**
 * Attaches files to a form: uploads the files the user chooses into one of Site's file containers (`SiteFileUploadService`),
 * shows the files attached with their address, and takes one out again. It is a form control whose value is the list of
 * the files (`SiteStoredFile`), which is what a file field stores; the share image takes `value[0].url`.
 *
 * Each file is checked against the container's size and type limits before it is sent - Blob Management checks again
 * when it arrives - and the files chosen are uploaded one after another, so one that fails does not stop the others.
 * Removing a file takes it out of the value only; a file uploaded during an edit that the saved form does not hold is
 * deleted by the page's `SiteUploadCleanup`, when it provides one.
 */
@Component({
  selector: 'site-file-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CoreModule, FormatFileSizePipe],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => SiteFileInputComponent), multi: true }],
  template: `
    <input
      #picker
      type="file"
      class="d-none"
      [attr.accept]="accept()"
      [multiple]="multiple()"
      (change)="upload($event)"
    />
    <button
      type="button"
      class="btn btn-sm btn-outline-primary"
      [disabled]="disabled() || uploading() || !containerName()"
      (click)="picker.click()"
    >
      @if (uploading()) {
        <span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>
      } @else {
        <i class="fa fa-upload me-1" aria-hidden="true"></i>
      }
      {{ 'Site::Files:Upload' | abpLocalization }}
    </button>

    @if (value().length > 0) {
      <ul class="list-group mt-2">
        @for (file of value(); track file.id) {
          <li class="list-group-item d-flex align-items-center gap-2">
            <i class="fa fa-file text-muted" aria-hidden="true"></i>
            @if (file.url) {
              <a class="flex-grow-1 text-break" [href]="urls.toDisplay(file.url)" target="_blank" rel="noopener">{{
                file.name
              }}</a>
            } @else {
              <span class="flex-grow-1 text-break">{{ file.name }}</span>
            }
            @if (file.size) {
              <small class="text-muted">{{ file.size | formatFileSize }}</small>
            }
            <button
              type="button"
              class="btn btn-sm btn-outline-danger"
              [disabled]="disabled()"
              [attr.aria-label]="'Site::Files:Remove' | abpLocalization"
              (click)="remove(file)"
            >
              <i class="fa fa-times" aria-hidden="true"></i>
            </button>
          </li>
        }
      </ul>
    }
  `,
})
export class SiteFileInputComponent implements ControlValueAccessor {
  private readonly files = inject(FileAdminService);
  private readonly uploads = inject(SiteFileUploadService);
  private readonly toaster = inject(ToasterService);
  private readonly cleanup = inject(SiteUploadCleanup, { optional: true });
  private readonly fileSize = new FormatFileSizePipe();
  protected readonly urls = inject(SiteFileUrlService);

  /** One of Site's file containers: `images`, `attachments` or `theme-assets`. */
  readonly containerName = input<string | null>(null);

  /** Whether several files may be attached; otherwise an upload replaces the file attached. */
  readonly multiple = input(false);

  /** The files attached. */
  readonly value = model<SiteStoredFile[]>([]);

  protected readonly disabled = signal(false);
  protected readonly uploading = signal(false);

  /** The container's limits, for checking a file before it is sent; Blob Management checks again on upload. */
  private readonly container = signal<SiteFileContainerDto | null>(null);

  /** The file input's `accept`: the container's allowed extensions; `null` - no attribute - for any. */
  protected readonly accept = computed(() => {
    const extensions = this.container()?.allowedFileExtensions ?? [];
    return extensions.length > 0 ? extensions.join(',') : null;
  });

  private onChange: (value: SiteStoredFile[]) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  constructor() {
    effect(onCleanup => {
      const name = this.containerName();
      this.container.set(null);
      if (!name) {
        return;
      }

      // Without the limits Blob Management's own checks on upload still apply, so a failure here is not shown.
      const subscription = this.files
        .getContainers({ skipHandleError: true })
        .pipe(catchError(() => EMPTY))
        .subscribe(result => this.container.set((result.items ?? []).find(container => container.name === name) ?? null));
      onCleanup(() => subscription.unsubscribe());
    });
  }

  /**
   * Uploads the files chosen, one after another, leaving out any the container would refuse for its size or its type.
   * A file that fails does not stop the others; each one uploaded is attached as soon as it is kept.
   */
  upload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const containerName = this.containerName();
    const chosen = Array.from(input.files ?? []);
    input.value = '';
    if (!containerName) {
      return;
    }

    const files = chosen.filter(file => this.accepts(file));
    if (files.length === 0) {
      return;
    }

    // ABP's error handler has shown each failure already; the batch goes on and one toast counts them at the end.
    let failed = 0;
    this.uploading.set(true);
    from(files)
      .pipe(
        concatMap(file =>
          this.uploads.upload(file, containerName).pipe(
            tap(uploaded => {
              this.cleanup?.uploaded(uploaded);
              this.setValue(this.multiple() ? [...this.value(), uploaded] : [uploaded]);
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
            this.toaster.error('Site::Files:UploadFailed', '', {
              messageLocalizationParams: [String(failed), String(files.length)],
            });
          }
        }),
      )
      .subscribe();
  }

  /** Takes the file out of the value; the file itself stays where it is. */
  remove(file: SiteStoredFile): void {
    this.setValue(this.value().filter(item => item.id !== file.id));
  }

  writeValue(value: SiteStoredFile[] | null): void {
    this.value.set(value ?? []);
  }

  registerOnChange(fn: (value: SiteStoredFile[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  /** Sets the files attached and reports them to the form. */
  private setValue(files: SiteStoredFile[]): void {
    this.value.set(files);
    this.onChange(files);
    this.onTouched();
  }

  private accepts(file: File): boolean {
    const maxFileSize = this.container()?.maxFileSize ?? 0;
    if (maxFileSize > 0 && file.size > maxFileSize) {
      this.toaster.error('Site::Files:FileTooLarge', '', {
        messageLocalizationParams: [file.name, this.fileSize.transform(maxFileSize)],
      });
      return false;
    }

    const extensions = this.container()?.allowedFileExtensions ?? [];
    const name = file.name.toLowerCase();
    if (extensions.length > 0 && !extensions.some(extension => name.endsWith(extension.toLowerCase()))) {
      this.toaster.error('Site::Files:FileTypeNotAllowed', '', { messageLocalizationParams: [file.name] });
      return false;
    }

    return true;
  }
}
