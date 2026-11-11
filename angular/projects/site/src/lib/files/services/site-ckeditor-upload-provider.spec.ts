import { TestBed } from '@angular/core/testing';
import { EnvironmentService, SessionStateService } from '@abp/ng.core';
import { DriveFileService } from '@dignite/ng.drive';
import { firstValueFrom, of } from 'rxjs';
import { SiteCKEditorUploadProvider } from './site-ckeditor-upload-provider';

describe('SiteCKEditorUploadProvider', () => {
  it('uploads the image to the Drive container as a form and resolves to Site\'s address of it on the API host', async () => {
    const create = vi.fn().mockReturnValue(of({ url: '/api/drive/public/images/abc.webp?__tenant=t1' }));
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionStateService, useValue: { getTenant: () => undefined } },
        { provide: DriveFileService, useValue: { create } },
      ],
    });
    TestBed.inject(EnvironmentService).setState({
      apis: { default: { url: 'https://default.example' }, SiteAdmin: { url: 'https://api.example/' } },
    } as never);

    const provider = TestBed.inject(SiteCKEditorUploadProvider);
    const url = await firstValueFrom(provider.upload(new File(['x'], 'photo.png'), 'images'));

    // Absolute in the editor's model; siteCKEditorConfigContributor saves it relative.
    expect(url).toBe('https://api.example/api/site-public/files/images/abc.webp');
    const body = create.mock.calls[0][0] as FormData;
    expect(body.get('containerName')).toBe('images');
    expect(body.get('file')).toBeInstanceOf(File);
  });

  it('fails when the Drive returns no public address', async () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionStateService, useValue: { getTenant: () => undefined } },
        { provide: DriveFileService, useValue: { create: () => of({}) } },
      ],
    });

    const provider = TestBed.inject(SiteCKEditorUploadProvider);

    await expect(firstValueFrom(provider.upload(new File(['x'], 'photo.png'), 'images'))).rejects.toThrow();
  });
});
