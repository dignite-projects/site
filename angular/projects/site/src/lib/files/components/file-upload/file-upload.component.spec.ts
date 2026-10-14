import { ChangeDetectionStrategy, Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';

import { FileUploadComponent } from './file-upload.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FileAdminService } from '../../../proxy/dignite/site/admin/files';
import { ObjectUrlService } from '../../services/object-url.service';

describe('FileUploadComponent', () => {
  let component: FileUploadComponent;
  let fixture: ComponentFixture<FileUploadComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileUploadComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileUploadComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should finish preparing every selected file before returning', async () => {
    const imageUrlService = TestBed.inject(ObjectUrlService);
    imageUrlService.get = () => 'blob:test-preview';
    const files = [new File(['content'], 'test.txt', { type: 'text/plain' })] as any[];

    await (component as any).addFiles(files);

    expect(files[0].src).toBe('blob:test-preview');
    expect(files[0].fileSize).toBeTruthy();
    expect(component.filesTableData).toEqual(files);
  });
});

/**
 * A host places this under the layout's OnPush ancestors (`abp-router-outlet`, ...). The container's real
 * size limit arrives in an HTTP callback; a plain-field write there never marks those for check, so the
 * "exceeds the maximum size" hint kept showing for a file the container actually accepts. See
 * `content-types.component.spec.ts`.
 */
@Component({
  selector: 'lib-test-on-push-ancestor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FileUploadComponent],
  template: '<site-file-upload fileContainerName="images" [fileData]="files" />',
})
class OnPushAncestorComponent {
  /** 2MB: over the pre-fetch 1MB default, under the 5MB the container reports. */
  readonly files = [{ name: 'photo.png', size: 2 * 1024 * 1024 }];
}

@Component({
  selector: 'lib-test-root',
  imports: [OnPushAncestorComponent],
  template: '<lib-test-on-push-ancestor />',
})
class RootComponent {}

describe('FileUploadComponent under an OnPush ancestor', () => {
  const containerConfiguration$ = new Subject<{ maxBlobSize: number }>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
      providers: [
        {
          provide: FileAdminService,
          useValue: { getContainerConfiguration: () => containerConfiguration$ },
        },
      ],
    });
  });

  it('drops the size warning once the container reports a larger limit, with no event to mark the ancestors', () => {
    const fixture = TestBed.createComponent(RootComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.text-danger')).toHaveLength(1);

    containerConfiguration$.next({ maxBlobSize: 5 * 1024 * 1024 });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.text-danger')).toHaveLength(0);
  });
});
