import { Component, Input, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IMAGE_TYPE_OPTIONS } from './models';
import { sizedFileUrl } from '../services/site-file-url';

/**
 * A thumbnail (image) or type icon (anything else) for one file. Site's file containers are publicly
 * readable, so an image is shown straight from its public address (`/api/site-public/files/...`), asked
 * for at the thumbnail's size so the browser does not download the full upload.
 */
@Component({
  selector: 'site-file-preview',
  templateUrl: './file-preview.component.html',
  styleUrls: ['./file-preview.component.scss'],
  imports: [CommonModule],
})
export class FilePreviewComponent implements OnChanges {
  @Input() width = '100px';
  /** The file's address: the public read endpoint's, or a local `blob:`/`data:` URL for a file not uploaded yet. */
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

  ngOnChanges(): void {
    this.updateFileType();
    this.displaySrc = this.isImage ? sizedFileUrl(this.src, this.resizeWidth, this.resizeHeight) : this.src;
  }

  private updateFileType() {
    const fileName = this.name || '';
    const fileType = this.type || '';
    if (!this.type) {
      this.type = fileName.includes('.7z') ? '7z' : '';
    }
    this.isImage = fileType.includes('image/');
    this.isAudio = fileType.includes('audio/');
    this.isVideo = fileType.includes('video/');
  }

  /** Falls back to a generic file icon when the type has no dedicated one. */
  getFileIconClass(): string {
    return IMAGE_TYPE_OPTIONS.find(item => item.type === this.type)?.icon || 'fa fa-file-o';
  }
}
