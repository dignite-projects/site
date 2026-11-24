import { Component, Input, OnChanges, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IMAGE_TYPE_OPTIONS } from './models';

const DEFAULT_ICON = 'fa fa-file-o';
import { sizedFileUrl } from '../services/site-file-url';
import { SiteFileUrlService } from '../services/site-file-url.service';

/**
 * A thumbnail (image) or type icon (anything else) for one file. Site's file containers are publicly
 * readable, so an image is shown straight from its public address (Blob Management's `/blobs/...`), asked
 * for at a size near the thumbnail's so the browser does not download the full upload. The address is stored
 * relative; it is shown - and linked - from the host this UI reaches it at (`SiteFileUrlService`).
 */
@Component({
  selector: 'site-file-preview',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './file-preview.component.html',
  styleUrls: ['./file-preview.component.scss'],
  imports: [CommonModule],
})
export class FilePreviewComponent implements OnChanges {
  private readonly fileUrls = inject(SiteFileUrlService);

  @Input() width = '100px';
  /** The file's address: Blob Management's public read, or a local `blob:`/`data:` URL for a file not uploaded yet. */
  @Input() src = '';
  @Input() type = '';
  @Input() name = '';
  @Input() className = '';
  /** Server-side resize for the thumbnail (cropped to fill when both are set). Ignored for a local URL. */
  @Input() resizeWidth?: number;
  @Input() resizeHeight?: number;

  isImage = true;
  isAudio = false;
  isVideo = false;
  displaySrc = '';
  /** The icon of a file that is not shown itself, by its type. */
  iconClass = DEFAULT_ICON;
  /** {@link src} where this UI can reach it - the download link. */
  linkHref = '';

  ngOnChanges(): void {
    this.updateFileType();
    this.linkHref = this.fileUrls.toDisplay(this.src);
    this.displaySrc = this.isImage ? sizedFileUrl(this.linkHref, this.resizeWidth, this.resizeHeight) : this.linkHref;
  }

  private updateFileType() {
    const fileType = this.type || '';
    this.isImage = fileType.includes('image/');
    this.isAudio = fileType.includes('audio/');
    this.isVideo = fileType.includes('video/');

    // A 7z archive has no MIME type of its own; its name tells it.
    const iconType = fileType || ((this.name || '').includes('.7z') ? '7z' : '');
    this.iconClass = IMAGE_TYPE_OPTIONS.find(item => item.type === iconType)?.icon ?? DEFAULT_ICON;
  }
}
