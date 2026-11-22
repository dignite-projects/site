import { refuseFile } from './site-file-limits';

describe('refuseFile', () => {
  const limits = { maxFileSize: 1024, allowedFileExtensions: ['.png', '.webp'] };

  function file(name: string, size: number): File {
    return new File([new Uint8Array(size)], name);
  }

  it('lets through a file within the size and of an allowed type, whatever the case of its extension', () => {
    expect(refuseFile(file('a.png', 10), limits)).toBeNull();
    expect(refuseFile(file('B.WEBP', 1024), limits)).toBeNull();
  });

  it('refuses a file over the size, with its name and the limit', () => {
    const refusal = refuseFile(file('big.png', 2048), limits);

    expect(refusal?.key).toBe('Site::Files:FileTooLarge');
    expect(refusal?.params).toEqual(['big.png', '1.0 KB']);
  });

  it('refuses a type the container does not take', () => {
    const refusal = refuseFile(file('doc.pdf', 10), limits);

    expect(refusal).toEqual({ key: 'Site::Files:FileTypeNotAllowed', params: ['doc.pdf'] });
  });

  it('refuses nothing when the limits are unknown or say any type and size', () => {
    expect(refuseFile(file('doc.pdf', 5_000_000), null)).toBeNull();
    expect(refuseFile(file('doc.pdf', 5_000_000), { maxFileSize: 0, allowedFileExtensions: [] })).toBeNull();
  });
});
