import { absoluteFileUrl, isSiteFileUrl, relativeFileUrl, roundUpImageSize, sizedFileUrl } from './site-file-url';

describe('isSiteFileUrl', () => {
  it('recognizes Blob Management addresses, relative or on any host, and earlier Site addresses', () => {
    expect(isSiteFileUrl('/blobs/images/abc.webp?__tenant=t1')).toBe(true);
    expect(isSiteFileUrl('https://blobs.example/blobs/attachments/a.pdf')).toBe(true);
    expect(isSiteFileUrl('/api/site-public/files/images/abc')).toBe(true);
  });

  it('recognizes nothing else', () => {
    expect(isSiteFileUrl('https://cdn.example/a.png')).toBe(false);
    expect(isSiteFileUrl('https://cdn.example/media/blobs/a.png')).toBe(false);
    expect(isSiteFileUrl('/blobs/')).toBe(false);
    expect(isSiteFileUrl('blob:http://localhost/x')).toBe(false);
    expect(isSiteFileUrl(undefined)).toBe(false);
  });
});

describe('sizedFileUrl', () => {
  const file = 'https://bm.example/blobs/images/abc.webp?__tenant=t1';

  it('asks a Site file for the size, keeping the tenant', () => {
    expect(sizedFileUrl(file, 1200, 630)).toBe(`${file}&width=1200&height=630`);
  });

  it('rounds each side up to a size Blob Management serves', () => {
    expect(sizedFileUrl(file, 96, 96)).toBe(`${file}&width=128&height=128`);
    expect(sizedFileUrl(file, 120)).toBe(`${file}&width=128`);
    expect(roundUpImageSize(9000)).toBe(4096);
  });

  it('replaces a size already on the address', () => {
    expect(sizedFileUrl(`${file}&Width=10&HEIGHT=5`, 256)).toBe(`${file}&width=256`);
  });

  it('leaves other addresses and size-less calls alone', () => {
    expect(sizedFileUrl('blob:http://localhost/x', 120, 80)).toBe('blob:http://localhost/x');
    expect(sizedFileUrl('https://cdn.example/a.png', 120)).toBe('https://cdn.example/a.png');
    expect(sizedFileUrl(file)).toBe(file);
    expect(sizedFileUrl(undefined, 120)).toBe('');
  });
});

describe('absoluteFileUrl', () => {
  const bases = { blobManagement: 'https://bm.example/', site: 'https://api.example' };
  const managed = '/blobs/images/abc.webp?__tenant=t1';
  const earlier = '/api/site-public/files/images/abc';

  it('puts Blob Management address on its host, as it is', () => {
    expect(absoluteFileUrl(managed, bases, 't2')).toBe(`https://bm.example${managed}`);
    expect(absoluteFileUrl('/blobs/images/host.webp', bases, null)).toBe('https://bm.example/blobs/images/host.webp');
  });

  it('puts an earlier Site address on the Site API host, naming the signed-in tenant', () => {
    expect(absoluteFileUrl(earlier, bases, 't1')).toBe(`https://api.example${earlier}?__tenant=t1`);
    expect(absoluteFileUrl(`${earlier}?width=80`, bases, 't1')).toBe(`https://api.example${earlier}?width=80&__tenant=t1`);
    expect(absoluteFileUrl(`${earlier}?__tenant=t2`, bases, 't1')).toBe(`https://api.example${earlier}?__tenant=t2`);
    expect(absoluteFileUrl(earlier, bases, null)).toBe(`https://api.example${earlier}`);
  });

  it('leaves absolute, local and external addresses alone, and relative ones when there is no host', () => {
    expect(absoluteFileUrl(`https://bm.example${managed}`, bases)).toBe(`https://bm.example${managed}`);
    expect(absoluteFileUrl('blob:http://localhost/x', bases)).toBe('blob:http://localhost/x');
    expect(absoluteFileUrl('https://cdn.example/a.png', bases, 't1')).toBe('https://cdn.example/a.png');
    expect(absoluteFileUrl(managed, { blobManagement: '', site: '' })).toBe(managed);
    expect(absoluteFileUrl(undefined, bases)).toBe('');
  });
});

describe('relativeFileUrl', () => {
  const bases = { blobManagement: 'https://bm.example', site: 'https://api.example' };
  const managed = '/blobs/images/abc.webp?__tenant=t1';
  const earlier = '/api/site-public/files/images/abc';

  it('takes Blob Management host off, keeping the tenant its address names', () => {
    expect(relativeFileUrl(`https://bm.example${managed}`, bases, 't1')).toBe(managed);
  });

  it('takes the Site API host and the signed-in tenant off an earlier address', () => {
    expect(relativeFileUrl(`https://api.example${earlier}?__tenant=t1`, bases, 't1')).toBe(earlier);
    expect(relativeFileUrl(`https://api.example${earlier}?width=80&__tenant=t1`, bases, 't1')).toBe(`${earlier}?width=80`);
    expect(relativeFileUrl(`${earlier}?__tenant=t1`, bases, 't1')).toBe(earlier);
  });

  it('leaves another tenant, an empty tenant and other addresses alone', () => {
    expect(relativeFileUrl(`https://api.example${earlier}?__tenant=t2`, bases, 't1')).toBe(`${earlier}?__tenant=t2`);
    expect(relativeFileUrl(`https://api.example${earlier}?__tenant=`, bases, 't1')).toBe(`${earlier}?__tenant=`);
    expect(relativeFileUrl('https://cdn.example/a.png?__tenant=t1', bases, 't1')).toBe('https://cdn.example/a.png?__tenant=t1');
  });
});
