import { TestBed } from '@angular/core/testing';
import { EnvironmentService } from '@abp/ng.core';
import { firstValueFrom, of } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files';
import { SiteCKEditorUploadProvider } from './site-ckeditor-upload-provider';

describe('SiteCKEditorUploadProvider', () => {
  it('uploads the image into the given container as multipart and resolves to its address on the API host', async () => {
    const create = vi.fn().mockReturnValue(of({ url: '/api/site-public/files/site-images/abc?__tenant=' }));
    TestBed.configureTestingModule({
      providers: [{ provide: FileAdminService, useValue: { create } }],
    });
    TestBed.inject(EnvironmentService).setState({
      apis: { default: { url: 'https://default.example' }, SiteAdmin: { url: 'https://api.example/' } },
    } as never);

    const provider = TestBed.inject(SiteCKEditorUploadProvider);
    const url = await firstValueFrom(provider.upload(new File(['x'], 'photo.png'), 'site-images'));

    // Absolute in the editor's model; siteCKEditorConfigContributor saves it relative.
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
