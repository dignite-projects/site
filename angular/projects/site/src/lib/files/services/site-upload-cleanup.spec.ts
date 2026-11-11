import { TestBed } from '@angular/core/testing';
import { ConfigStateService } from '@abp/ng.core';
import { DriveFileDto, DriveFileService } from '@dignite/ng.drive';
import { firstValueFrom, of, throwError } from 'rxjs';
import { SiteUploadCleanup } from './site-upload-cleanup';

describe('SiteUploadCleanup', () => {
  const userId = 'user-1';
  let remove: ReturnType<typeof vi.fn>;

  function setUp(): SiteUploadCleanup {
    remove = vi.fn().mockReturnValue(of(undefined));
    TestBed.configureTestingModule({
      providers: [
        SiteUploadCleanup,
        { provide: DriveFileService, useValue: { delete: remove } },
        { provide: ConfigStateService, useValue: { getOne: () => ({ id: userId }) } },
      ],
    });
    return TestBed.inject(SiteUploadCleanup);
  }

  function file(id: string, overrides: Partial<DriveFileDto> = {}): DriveFileDto {
    return {
      id,
      creatorId: userId,
      creationTime: new Date(Date.now() + 1000).toISOString(),
      url: `/api/drive/public/images/${id}-blob.webp`,
      ...overrides,
    };
  }

  it('deletes a file uploaded during the edit and removed before the save', async () => {
    const cleanup = setUp();
    const uploaded = file('a');

    cleanup.changed([], [uploaded]);
    cleanup.changed([uploaded], []);
    await firstValueFrom(cleanup.saved({ field: [] }));

    expect(remove).toHaveBeenCalledWith('a', { skipHandleError: true });
  });

  it('keeps a file the saved value still holds, by id or by its address', async () => {
    const cleanup = setUp();

    cleanup.changed([], [file('a'), file('b')]);
    await firstValueFrom(cleanup.saved({ files: [{ id: 'a' }], ogImage: '/api/site-public/files/images/b-blob.webp' }));

    expect(remove).not.toHaveBeenCalled();
  });

  it('never deletes a file that was in the form already, picked from elsewhere, or uploaded before the edit', async () => {
    const cleanup = setUp();
    const before = file('old');

    cleanup.changed([before], [before, file('other-user', { creatorId: 'someone-else' }), file('earlier', {
      creationTime: new Date(Date.now() - 60_000).toISOString(),
    })]);
    await firstValueFrom(cleanup.saved({}));

    expect(remove).not.toHaveBeenCalled();
  });

  it('starts over after a save, and does not fail the save when a delete fails', async () => {
    const cleanup = setUp();
    remove.mockReturnValue(throwError(() => new Error('403')));

    cleanup.changed([], [file('a')]);
    await expect(firstValueFrom(cleanup.saved({}))).resolves.toBeUndefined();
    remove.mockClear();
    await firstValueFrom(cleanup.saved({}));

    expect(remove).not.toHaveBeenCalled();
  });
});
