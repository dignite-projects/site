import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, SimpleChange } from '@angular/core';
import { By } from '@angular/platform-browser';
import { ConfirmationService, ModalComponent, ToasterService } from '@abp/ng.theme.shared';
import { Subject } from 'rxjs';

import { FileModalComponent } from './file-modal.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FileAdminService } from '../../../proxy/dignite/site/admin/files';
import type { FileDescriptorDto } from '../../../proxy/dignite/site/admin/files';

describe('FileModalComponent', () => {
  let component: FileModalComponent;
  let fixture: ComponentFixture<FileModalComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileModalComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should mark a failed upload without throwing', async () => {
    const file = { name: 'failed.txt', size: 1 } as any;
    const testComponent = Object.create(FileModalComponent.prototype) as FileModalComponent;
    (testComponent as any).sizeLimit = 1024;
    (testComponent as any).list = { get: () => undefined };
    (testComponent as any).uploadingFiles = [file];
    (testComponent as any).uploadFile = () => Promise.reject(new Error('upload failed'));

    await testComponent.onFileInputChange({ target: { files: [file] } } as any);
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(file.status).toBe(2);
  });

  it('should initialize the selection with an empty array when the input is undefined', () => {
    component.ngOnChanges({
      selectPickerFile: new SimpleChange(undefined, undefined, true),
    });

    expect(component.selectedFiles).toEqual([]);
  });

  it('should preserve the in-progress selection when another input changes', () => {
    const selectedFile = { id: 'selected' } as any;
    component.selectedFiles = [selectedFile];

    component.ngOnChanges({
      multiple: new SimpleChange(false, true, false),
    });

    expect(component.selectedFiles).toEqual([selectedFile]);
  });

  it('should clone the selection when selectPickerFile changes', () => {
    const selectedFile = { id: 'selected' };
    const inputFiles = [selectedFile];

    component.ngOnChanges({
      selectPickerFile: new SimpleChange(undefined, inputFiles, true),
    });

    expect(component.selectedFiles).toEqual(inputFiles);
    expect(component.selectedFiles).not.toBe(inputFiles);
  });

  it('should add a checked file to the selection', () => {
    const selectedFile = { id: 'selected' } as any;

    component.onRowCheckboxChange({ target: { checked: true } } as any, selectedFile, [selectedFile]);

    expect(component.selectedFiles).toHaveLength(1);
    expect(component.selectedFiles[0]).toBe(selectedFile);
  });

  it('should use the file list sort icon convention', () => {
    expect(component.tableSortIcons).toEqual({
      sortAscending: 'datatable-icon-down',
      sortDescending: 'datatable-icon-up',
    });
  });
});

/**
 * The secondary dialogs' `<abp-modal [(visible)]>` sit in this component's own view, under the layout's
 * OnPush ancestors (`abp-router-outlet`, ...). The close that follows a successful rename happens in an
 * HTTP callback; a plain-field write there never marked those for check, so the dialog stayed open until
 * an unrelated event did. See `content-types.component.spec.ts`.
 */
@Component({
  selector: 'site-test-on-push-ancestor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FileModalComponent],
  template: '<site-file-modal />',
})
class OnPushAncestorComponent {}

@Component({
  selector: 'site-test-root',
  imports: [OnPushAncestorComponent],
  template: '<site-test-on-push-ancestor />',
})
class RootComponent {}

describe('FileModalComponent under an OnPush ancestor', () => {
  const update$ = new Subject<unknown>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
      providers: [
        {
          provide: FileAdminService,
          useValue: { update: () => update$, getContainerConfiguration: () => update$ },
        },
        { provide: ToasterService, useValue: { success: () => undefined, error: () => undefined } },
        { provide: ConfirmationService, useValue: {} },
      ],
    });
  });

  it('closes the rename dialog once the rename succeeds, with no event to mark the ancestors', () => {
    const fixture = TestBed.createComponent(RootComponent);
    fixture.detectChanges();

    const modalElement = fixture.debugElement.query(By.directive(FileModalComponent));
    const modal = modalElement.componentInstance as FileModalComponent;
    // The three `abp-modal`s in template order: the picker itself, rename, move.
    const renameDialog = () =>
      fixture.debugElement.queryAll(By.directive(ModalComponent))[1].componentInstance as ModalComponent;

    // Opening comes from a click on a row's edit button, which marks the view for check - stand in for it.
    modal.onEditFileName({ id: 'file-1', name: 'old.png' } as FileDescriptorDto);
    modalElement.injector.get(ChangeDetectorRef).markForCheck();
    fixture.detectChanges();
    expect(renameDialog().visible()).toBe(true);

    // Closing happens in the HTTP callback: nothing marks the view for check.
    modal.onSubmitFileName();
    update$.next({});
    fixture.detectChanges();

    expect(renameDialog().visible()).toBe(false);
  });
});
