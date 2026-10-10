import { Component, Input } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { FlexFieldValue } from '@dignite/ng.flex-fields';
import { FilePreviewComponent } from '../../files/previews/file-preview.component';

/** The part of a picked file the view reads - what the picker denormalized into the value. */
interface FileFieldFileValue {
  url?: string;
  name?: string;
  mimeType?: string;
}

/** Displays a file field's value read-only: the first file's preview, plus a count. */
@Component({
  selector: 'site-file-view',
  templateUrl: './file-view.component.html',
  imports: [CoreModule, FilePreviewComponent],
})
export class FileViewComponent {
  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type (`FileExplorer`). */
  @Input() type?: string;

  @Input() value: unknown = '';

  get files(): FileFieldFileValue[] {
    return Array.isArray(this.value) ? this.value : [];
  }
}
