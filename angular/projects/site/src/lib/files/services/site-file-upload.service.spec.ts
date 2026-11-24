import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { firstValueFrom, lastValueFrom, of, throwError, toArray } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import { SiteFileUploadService } from './site-file-upload.service';

describe('SiteFileUploadService', () => {
  const ticket = { fileId: 'f1', uploadUrl: '/api/blob-management/uploads/T1' };

  function setUp(overrides: Partial<Record<'createUploadTicket' | 'confirm', unknown>> = {}) {
    const files = {
      apiName: 'SiteAdmin',
      createUploadTicket: vi.fn().mockReturnValue(of(ticket)),
      confirm: vi.fn().mockReturnValue(
        of({ id: 'f1', containerName: 'images', name: 'photo.webp', mimeType: 'image/webp', size: 12, url: '/blobs/images/b.webp', isPending: false }),
      ),
      ...overrides,
    };
    const request = vi.fn().mockReturnValue(of({ id: 'f1', isPending: true }));
    const toaster = { error: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        { provide: FileAdminService, useValue: files },
        { provide: RestService, useValue: { request } },
        { provide: ToasterService, useValue: toaster },
      ],
    });
    return { files, request, toaster, service: TestBed.inject(SiteFileUploadService) };
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
    expect(config).toEqual({ apiName: 'BlobManagement' });
    expect(files.confirm).toHaveBeenCalledWith('f1');
    expect(stored).toEqual({
      id: 'f1',
      containerName: 'images',
      name: 'photo.webp',
      mimeType: 'image/webp',
      size: 12,
      url: '/blobs/images/b.webp',
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

  describe('uploadAll', () => {
    it('leaves out a file the container refuses, and says why', async () => {
      const { files, toaster, service } = setUp();

      const stored = await lastValueFrom(
        service.uploadAll([new File(['x'], 'notes.exe'), new File(['x'], 'photo.jpg')], 'images', { allowedFileExtensions: ['.jpg'] }).pipe(toArray()),
      );

      expect(stored.map(file => file.id)).toEqual(['f1']);
      expect(files.createUploadTicket).toHaveBeenCalledTimes(1);
      expect(toaster.error).toHaveBeenCalledWith('Site::Files:FileTypeNotAllowed', '', expect.anything());
    });

    it('goes on past a file that fails, and counts the failures once at the end', async () => {
      const { request, toaster, service } = setUp();
      request.mockReturnValueOnce(throwError(() => new Error('too large'))).mockReturnValue(of({}));

      const stored = await lastValueFrom(
        service.uploadAll([new File(['x'], 'a.jpg'), new File(['x'], 'b.jpg')], 'images', null).pipe(toArray()),
      );

      expect(stored).toHaveLength(1);
      expect(toaster.error).toHaveBeenCalledTimes(1);
      expect(toaster.error).toHaveBeenCalledWith('Site::Files:UploadFailed', '', { messageLocalizationParams: ['1', '2'] });
    });
  });
});
