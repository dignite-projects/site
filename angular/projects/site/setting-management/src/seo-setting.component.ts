import { CoreModule, PermissionDirective } from '@abp/ng.core';
import { ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  Dignite,
  FileModalComponent,
  FilePreviewComponent,
  OG_IMAGE_CONTAINER_NAME,
  OG_IMAGE_HEIGHT,
  OG_IMAGE_WIDTH,
  eSitePolicyNames,
} from '@dignite/ng.site';

/**
 * The site's SEO settings, as a tab of Setting Management's page. For now the default share image: the one a page
 * without a share image of its own is shared with. It is picked from the file library the way a page's own share
 * image is (the `images` container); an address someone stored by other means shows and can be replaced or removed the
 * same way. The server stores what it is given, and empty clears it. Without the update permission the tab only shows.
 */
@Component({
  selector: 'site-seo-setting',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './seo-setting.component.html',
  imports: [CoreModule, ThemeSharedModule, PermissionDirective, FileModalComponent, FilePreviewComponent],
})
export class SeoSettingComponent {
  private readonly service = inject(Dignite.Site.Admin.Seo.SiteSeoSettingsAdminService);
  private readonly toaster = inject(ToasterService);

  readonly policies = eSitePolicyNames;
  readonly imageContainerName = OG_IMAGE_CONTAINER_NAME;
  readonly imageWidth = OG_IMAGE_WIDTH;
  readonly imageHeight = OG_IMAGE_HEIGHT;

  // Signals, not fields: the layout shell is OnPush, and a value set in a subscribe callback would not repaint.
  readonly image = signal('');
  readonly saved = signal('');
  readonly pickerOpen = signal(false);
  readonly saving = signal(false);

  readonly isDirty = computed(() => this.image() !== this.saved());

  constructor() {
    this.load();
  }

  load() {
    this.service.get().subscribe(result => this.show(result.defaultShareImage ?? ''));
  }

  onPicked(files: Dignite.Site.Admin.Files.FileDescriptorDto[]) {
    const url = files[0]?.url;
    if (url) {
      this.image.set(url);
    }
  }

  removeImage() {
    this.image.set('');
  }

  save() {
    if (this.saving() || !this.isDirty()) {
      return;
    }

    this.saving.set(true);
    this.service.update({ defaultShareImage: this.image() }).subscribe({
      next: result => {
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
  }
}
