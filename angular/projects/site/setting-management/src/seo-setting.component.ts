import { CoreModule, PermissionDirective } from '@abp/ng.core';
import { ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DriveFileDto, DriveFileInputComponent } from '@dignite/ng.drive';
import {
  Dignite,
  FilePreviewComponent,
  OG_IMAGE_CONTAINER_NAME,
  OG_IMAGE_HEIGHT,
  OG_IMAGE_WIDTH,
  SiteUploadCleanup,
  eSitePolicyNames,
  fileOfUrl,
} from '@dignite/ng.site';

/**
 * The site's SEO settings, as a tab of Setting Management's page. For now the default share image: the one a page
 * without a share image of its own is shared with. It is uploaded with the Drive's file input the way a page's own
 * share image is (the `images` container); an address someone stored by other means shows and can be replaced or
 * removed the same way. The server stores what it is given, and empty clears it. Without the update permission the tab
 * only shows. An image uploaded and removed again before saving is deleted once the setting is saved
 * (`SiteUploadCleanup`).
 */
@Component({
  selector: 'site-seo-setting',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './seo-setting.component.html',
  imports: [CoreModule, ThemeSharedModule, PermissionDirective, DriveFileInputComponent, FilePreviewComponent],
  providers: [SiteUploadCleanup],
})
export class SeoSettingComponent {
  private readonly service = inject(Dignite.Site.Admin.Seo.SiteSeoSettingsAdminService);
  private readonly toaster = inject(ToasterService);
  private readonly cleanup = inject(SiteUploadCleanup);

  readonly policies = eSitePolicyNames;
  readonly imageContainerName = OG_IMAGE_CONTAINER_NAME;
  readonly imageWidth = OG_IMAGE_WIDTH;
  readonly imageHeight = OG_IMAGE_HEIGHT;

  // Signals, not fields: the layout shell is OnPush, and a value set in a subscribe callback would not repaint.
  readonly image = signal('');
  readonly saved = signal('');
  /** The image as the file input's value: the uploaded file, or the stored address wrapped as one. */
  readonly imageFiles = signal<DriveFileDto[]>([]);
  readonly saving = signal(false);

  readonly isDirty = computed(() => this.image() !== this.saved());

  constructor() {
    this.load();
  }

  load() {
    this.service.get().subscribe(result => this.show(result.defaultShareImage ?? ''));
  }

  onImageChange(files: DriveFileDto[]) {
    this.cleanup.changed(this.imageFiles(), files);
    this.imageFiles.set(files);
    this.image.set(files[0]?.url ?? '');
  }

  save() {
    if (this.saving() || !this.isDirty()) {
      return;
    }

    this.saving.set(true);
    this.service.update({ defaultShareImage: this.image() }).subscribe({
      next: result => {
        this.cleanup.saved({ defaultShareImage: result.defaultShareImage }).subscribe();
        this.show(result.defaultShareImage ?? '');
        this.saving.set(false);
        this.toaster.success('AbpUi::SavedSuccessfully');
      },
      error: () => this.saving.set(false),
    });
  }

  private show(image: string) {
    this.image.set(image);
    this.saved.set(image);
    this.imageFiles.set(fileOfUrl(image));
  }
}
