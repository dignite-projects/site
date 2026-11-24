import { TestBed } from '@angular/core/testing';
import { EnvironmentService, SessionStateService } from '@abp/ng.core';
import { firstValueFrom, of, throwError } from 'rxjs';
import { SiteCKEditorUploadProvider } from './site-ckeditor-upload-provider';
import { SiteFileUploadService } from './site-file-upload.service';
import { SiteUploadCleanup } from './site-upload-cleanup';

describe('SiteCKEditorUploadProvider', () => {
  it("uploads the image to the container and resolves to Site's address of it on the API host", async () => {
    const upload = vi.fn().mockReturnValue(of({ id: 'f1', name: 'abc.webp', url: '/api/site-public/files/images/abc.webp' }));
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionStateService, useValue: { getTenant: () => undefined } },
        { provide: SiteFileUploadService, useValue: { upload } },
      ],
    });
    TestBed.inject(EnvironmentService).setState({
      apis: { default: { url: 'https://default.example' }, SiteAdmin: { url: 'https://api.example/' } },
    } as never);

    const provider = TestBed.inject(SiteCKEditorUploadProvider);
    const file = new File(['x'], 'photo.png');
    const url = await firstValueFrom(provider.upload(file, 'images'));

    // Absolute in the editor's model; siteCKEditorConfigContributor saves it relative.
    expect(url).toBe('https://api.example/api/site-public/files/images/abc.webp');
    expect(upload).toHaveBeenCalledWith(file, 'images');
  });

  it("reports the stored file to the page's cleanup, so an image the saved text does not hold is deleted", async () => {
    const stored = { id: 'f1', name: 'abc.webp', url: '/blobs/images/abc.webp' };
    const uploaded = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionStateService, useValue: { getTenant: () => undefined } },
        { provide: SiteFileUploadService, useValue: { upload: () => of(stored) } },
        { provide: SiteUploadCleanup, useValue: { uploaded } },
      ],
    });

    await firstValueFrom(TestBed.inject(SiteCKEditorUploadProvider).upload(new File(['x'], 'photo.png'), 'images'));

    expect(uploaded).toHaveBeenCalledWith(stored);
  });

  it('reports nothing when the upload fails', async () => {
    const uploaded = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionStateService, useValue: { getTenant: () => undefined } },
        { provide: SiteFileUploadService, useValue: { upload: () => throwError(() => new Error('refused')) } },
        { provide: SiteUploadCleanup, useValue: { uploaded } },
      ],
    });

    await expect(
      firstValueFrom(TestBed.inject(SiteCKEditorUploadProvider).upload(new File(['x'], 'photo.png'), 'images')),
    ).rejects.toThrow('refused');
    expect(uploaded).not.toHaveBeenCalled();
  });

  it('fails when the file comes back with no public address', async () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionStateService, useValue: { getTenant: () => undefined } },
        { provide: SiteFileUploadService, useValue: { upload: () => of({ id: 'f1', name: 'abc.webp', url: '' }) } },
      ],
    });

    const provider = TestBed.inject(SiteCKEditorUploadProvider);

    await expect(firstValueFrom(provider.upload(new File(['x'], 'photo.png'), 'images'))).rejects.toThrow();
  });
});
