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
});
