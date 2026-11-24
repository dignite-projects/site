import { TestBed } from '@angular/core/testing';
import { firstValueFrom, Observable, of, throwError } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import { SiteStoredFile } from './site-stored-file';
import { SiteUploadCleanup } from './site-upload-cleanup';

describe('SiteUploadCleanup', () => {
  let remove: ReturnType<typeof vi.fn>;

  function setUp(deleteResult: Observable<unknown> = of(undefined)): SiteUploadCleanup {
    remove = vi.fn().mockReturnValue(deleteResult);
    TestBed.configureTestingModule({
      providers: [SiteUploadCleanup, { provide: FileAdminService, useValue: { delete: remove } }],
    });
    return TestBed.inject(SiteUploadCleanup);
  }

  function file(id: string, overrides: Partial<SiteStoredFile> = {}): SiteStoredFile {
    return { id, name: `${id}.webp`, url: `/api/site-public/files/images/${id}-blob.webp`, ...overrides };
  }

  it('deletes a file uploaded during the edit that the saved value does not hold', async () => {
    const cleanup = setUp();

    cleanup.uploaded(file('a'));
    await firstValueFrom(cleanup.saved({ field: [] }));

    expect(remove).toHaveBeenCalledWith('a', { skipHandleError: true });
  });

  it('keeps a file the saved value still holds, by id or by its address', async () => {
    const cleanup = setUp();

    cleanup.uploaded(file('a'));
    cleanup.uploaded(file('b'));
    await firstValueFrom(cleanup.saved({ files: [{ id: 'a' }], ogImage: '/api/site-public/files/images/b-blob.webp' }));

    expect(remove).not.toHaveBeenCalled();
  });

  it('starts over after a save: a file is deleted once, not by every later save', async () => {
    const cleanup = setUp();

    cleanup.uploaded(file('a'));
    await firstValueFrom(cleanup.saved({}));
    await firstValueFrom(cleanup.saved({}));

    expect(remove).toHaveBeenCalledTimes(1);
  });

  it('does not let a file that cannot be deleted fail the save', async () => {
    const cleanup = setUp(throwError(() => new Error('gone')));

    cleanup.uploaded(file('a'));

    await expect(firstValueFrom(cleanup.saved({}))).resolves.toBeUndefined();
  });

  it('says whether an upload is waiting for a save to judge it', async () => {
    const cleanup = setUp();

    expect(cleanup.hasUploads()).toBe(false);

    cleanup.uploaded(file('a'));
    expect(cleanup.hasUploads()).toBe(true);

    await firstValueFrom(cleanup.saved({ files: [{ id: 'a' }] }));
    expect(cleanup.hasUploads()).toBe(false);
  });

  describe('discard', () => {
    it('deletes every file uploaded during the edit, whatever a value would hold', async () => {
      const cleanup = setUp();

      cleanup.uploaded(file('a'));
      cleanup.uploaded(file('b'));
      await firstValueFrom(cleanup.discard());

      expect(remove).toHaveBeenCalledTimes(2);
      expect(remove).toHaveBeenCalledWith('a', { skipHandleError: true });
      expect(remove).toHaveBeenCalledWith('b', { skipHandleError: true });
    });

    it('deletes nothing when nothing was uploaded', async () => {
      const cleanup = setUp();

      await firstValueFrom(cleanup.discard());

      expect(remove).not.toHaveBeenCalled();
    });

    it('starts over: a file is deleted once, by neither a later discard nor a later save', async () => {
      const cleanup = setUp();

      cleanup.uploaded(file('a'));
      await firstValueFrom(cleanup.discard());
      await firstValueFrom(cleanup.discard());
      await firstValueFrom(cleanup.saved({}));

      expect(remove).toHaveBeenCalledTimes(1);
    });

    it('does not keep what a save held out of the files to discard', async () => {
      const cleanup = setUp();

      cleanup.uploaded(file('a'));
      await firstValueFrom(cleanup.saved({ files: [{ id: 'a' }] }));
      await firstValueFrom(cleanup.discard());

      expect(remove).not.toHaveBeenCalled();
    });

    it('does not let a file that cannot be deleted fail it', async () => {
      const cleanup = setUp(throwError(() => new Error('gone')));

      cleanup.uploaded(file('a'));

      await expect(firstValueFrom(cleanup.discard())).resolves.toBeUndefined();
    });

    it('does not let a delete that throws before it returns fail it either', async () => {
      const cleanup = setUp();
      remove.mockImplementation(() => {
        throw new Error('no API configured');
      });

      cleanup.uploaded(file('a'));

      await expect(firstValueFrom(cleanup.discard())).resolves.toBeUndefined();
    });
  });
});
