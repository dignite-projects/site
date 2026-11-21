import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { firstValueFrom, of, throwError } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import { SiteFileUploadService } from './site-file-upload.service';

describe('SiteFileUploadService', () => {
  const ticket = { fileId: 'f1', uploadUrl: '/api/blob-management/uploads/T1' };

  function setUp(overrides: Partial<Record<'createUploadTicket' | 'confirm', unknown>> = {}) {
    const files = {
      apiName: 'SiteAdmin',
      createUploadTicket: vi.fn().mockReturnValue(of(ticket)),
      confirm: vi.fn().mockReturnValue(
        of({ id: 'f1', containerName: 'images', name: 'photo.webp', mimeType: 'image/webp', size: 12, url: '/api/site-public/files/images/b.webp', isPending: false }),
      ),
      ...overrides,
    };
    const request = vi.fn().mockReturnValue(of({ id: 'f1', isPending: true }));
    TestBed.configureTestingModule({
      providers: [
        { provide: FileAdminService, useValue: files },
        { provide: RestService, useValue: { request } },
      ],
    });
    return { files, request, service: TestBed.inject(SiteFileUploadService) };
  }

  it('asks for a ticket, posts the file to its address, confirms it and returns the stored file', async () => {
    const { files, request, service } = setUp();
    const file = new File(['x'], 'photo.jpg');

    const stored = await firstValueFrom(service.upload(file, 'images'));

    expect(files.createUploadTicket).toHaveBeenCalledWith({ containerName: 'images' });
    const [call, config] = request.mock.calls[0];
    expect(call.method).toBe('POST');
    expect(call.url).toBe('/api/blob-management/uploads/T1');
    expect((call.body as FormData).get('file')).toBeInstanceOf(File);
    expect(config).toEqual({ apiName: 'SiteAdmin' });
    expect(files.confirm).toHaveBeenCalledWith('f1');
    expect(stored).toEqual({
      id: 'f1',
      containerName: 'images',
      name: 'photo.webp',
      mimeType: 'image/webp',
      size: 12,
      url: '/api/site-public/files/images/b.webp',
    });
  });

  it('files the upload in the directory it is given, when there is one', async () => {
    const { files, service } = setUp();

    await firstValueFrom(service.upload(new File(['x'], 'photo.jpg'), 'images', 'dir-1'));

    expect(files.createUploadTicket).toHaveBeenCalledWith({ containerName: 'images', directoryId: 'dir-1' });
  });

  it('does not confirm a file whose upload failed', async () => {
    const { files, request, service } = setUp();
    request.mockReturnValue(throwError(() => new Error('too large')));

    await expect(firstValueFrom(service.upload(new File(['x'], 'photo.jpg'), 'images'))).rejects.toThrow('too large');

    expect(files.confirm).not.toHaveBeenCalled();
  });
});
