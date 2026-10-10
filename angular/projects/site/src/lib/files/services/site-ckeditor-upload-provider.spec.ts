import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files';
import { SiteCKEditorUploadProvider } from './site-ckeditor-upload-provider';

describe('SiteCKEditorUploadProvider', () => {
  it('uploads the image into the given container as multipart and resolves to its public address', async () => {
    const create = vi.fn().mockReturnValue(
      of({ url: 'https://api.example/api/site-public/files/site-images/abc?__tenant=' }),
    );
    TestBed.configureTestingModule({
      providers: [{ provide: FileAdminService, useValue: { create } }],
    });

    const provider = TestBed.inject(SiteCKEditorUploadProvider);
    const url = await firstValueFrom(provider.upload(new File(['x'], 'photo.png'), 'site-images'));

    expect(url).toBe('https://api.example/api/site-public/files/site-images/abc?__tenant=');
    const input = create.mock.calls[0][0];
    expect(input.containerName).toBe('site-images');
    expect(input.file).toBeInstanceOf(FormData);
    expect((input.file as FormData).get('file')).toBeInstanceOf(File);
  });

  it('fails when the server returns no address', async () => {
    TestBed.configureTestingModule({
      providers: [{ provide: FileAdminService, useValue: { create: () => of({}) } }],
    });

    const provider = TestBed.inject(SiteCKEditorUploadProvider);

    await expect(firstValueFrom(provider.upload(new File(['x'], 'photo.png'), 'site-images'))).rejects.toThrow();
  });
});
