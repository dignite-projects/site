import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, throwError } from 'rxjs';
import { FileAdminService } from '../../proxy/dignite/site/admin/files/file-admin.service';
import { SiteFileContainersService } from './site-file-containers.service';

describe('SiteFileContainersService', () => {
  const images = { name: 'images', maxFileSize: 1024, allowedFileExtensions: ['.png'] };

  function setUp(getContainers: ReturnType<typeof vi.fn>) {
    TestBed.configureTestingModule({
      providers: [{ provide: FileAdminService, useValue: { getContainers } }],
    });
    return TestBed.inject(SiteFileContainersService);
  }

  it('asks the server once and shares the answer', async () => {
    const getContainers = vi.fn().mockReturnValue(of({ items: [images] }));
    const service = setUp(getContainers);

    expect(await firstValueFrom(service.list())).toEqual([images]);
    expect(await firstValueFrom(service.list())).toEqual([images]);

    expect(getContainers).toHaveBeenCalledTimes(1);
    expect(getContainers).toHaveBeenCalledWith({ skipHandleError: true });
  });

  it('does not keep a failure: the next call asks again', async () => {
    const getContainers = vi
      .fn()
      .mockReturnValueOnce(throwError(() => new Error('down')))
      .mockReturnValueOnce(of({ items: [images] }));
    const service = setUp(getContainers);

    await expect(firstValueFrom(service.list())).rejects.toThrow('down');
    expect(await firstValueFrom(service.list())).toEqual([images]);

    expect(getContainers).toHaveBeenCalledTimes(2);
  });

  it('reads no items as no containers', async () => {
    const service = setUp(vi.fn().mockReturnValue(of({})));

    expect(await firstValueFrom(service.list())).toEqual([]);
  });
});
