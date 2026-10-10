/* eslint-disable @angular-eslint/component-selector */
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { CoreModule } from '@abp/ng.core';
import { FileModalComponent } from '../file-modal/file-modal.component';
import { FormatFileSizePipe } from '../../pipes/format-file-size.pipe';
import { FilePreviewComponent } from '../../previews/file-preview.component';
import { FileDescriptorDto } from '../../../proxy/dignite/site/admin/files';

@Component({
  selector: 'site-file-picker',
  templateUrl: './file-picker.component.html',
  styleUrls: ['./file-picker.component.scss'],
  imports: [
    CoreModule,
    DragDropModule,
    FileModalComponent,
    FilePreviewComponent,
    FormatFileSizePipe,
  ],
})
export class FilePickerComponent implements OnChanges {
  @Input() multiple = false;

  /** No default: a caller that never specifies a container has a configuration bug, not an "Images" one. */
  @Input() fileContainerName = '';

  @Input() selectFormFile: FileDescriptorDto[] = [];

  @Output() selectedFileChange = new EventEmitter<FileDescriptorDto[]>();

  /** This component's single source of truth; also passed straight to the modal to reselect what's already picked. */
  fileShowTable: FileDescriptorDto[] = [];

  modalOpen = false;

  ngOnChanges(changes: SimpleChanges): void {
    const selectFormFileChange = changes['selectFormFile'];
    if (selectFormFileChange?.currentValue?.length > 0) {
      this.fileShowTable = selectFormFileChange.currentValue;
    }
  }

  onSelectFile(files: FileDescriptorDto[]): void {
    this.fileShowTable = structuredClone(files);
    this.selectedFileChange.emit(this.fileShowTable);
  }

  deleteFileTableItem(index: number): void {
    this.fileShowTable.splice(index, 1);
    this.selectedFileChange.emit(this.fileShowTable);
  }

  drop(event: CdkDragDrop<FileDescriptorDto[]>): void {
    moveItemInArray(this.fileShowTable, event.previousIndex, event.currentIndex);
    this.selectedFileChange.emit(this.fileShowTable);
  }
}
