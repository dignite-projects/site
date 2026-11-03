import { absoluteFileUrl, isSiteFileUrl, relativeFileUrl, sizedFileUrl } from './site-file-url';

describe('sizedFileUrl', () => {
  const file = 'https://api.example/api/site-public/files/images/abc?__tenant=';

  it('asks a Site file for the size, keeping the tenant', () => {
    expect(sizedFileUrl(file, 120, 80)).toBe(`${file}&Width=120&Height=80`);
  });

  it('replaces a size already on the address', () => {
    expect(sizedFileUrl(`${file}&width=10&HEIGHT=5`, 120)).toBe(`${file}&Width=120`);
  });

  it('leaves other addresses and size-less calls alone', () => {
    expect(sizedFileUrl('blob:http://localhost/x', 120, 80)).toBe('blob:http://localhost/x');
    expect(sizedFileUrl('https://cdn.example/a.png', 120)).toBe('https://cdn.example/a.png');
    expect(sizedFileUrl(file)).toBe(file);
    expect(sizedFileUrl(undefined, 120)).toBe('');
  });

  it('recognizes Site file addresses only', () => {
    expect(isSiteFileUrl(file)).toBe(true);
    expect(isSiteFileUrl('https://cdn.example/a.png')).toBe(false);
  });
});

describe('absoluteFileUrl', () => {
  const relative = '/api/site-public/files/images/abc?__tenant=';

  it('puts a relative Site file address on the API host', () => {
    expect(absoluteFileUrl(relative, 'https://api.example')).toBe(`https://api.example${relative}`);
    expect(absoluteFileUrl(relative, 'https://api.example/')).toBe(`https://api.example${relative}`);
  });

  it('leaves absolute, local and external addresses alone, and everything when there is no API host', () => {
    expect(absoluteFileUrl(`https://api.example${relative}`, 'https://api.example')).toBe(`https://api.example${relative}`);
    expect(absoluteFileUrl('blob:http://localhost/x', 'https://api.example')).toBe('blob:http://localhost/x');
    expect(absoluteFileUrl('https://cdn.example/a.png', 'https://api.example')).toBe('https://cdn.example/a.png');
    expect(absoluteFileUrl(relative, '')).toBe(relative);
    expect(absoluteFileUrl(undefined, 'https://api.example')).toBe('');
  });
});

describe('the tenant of a displayed address', () => {
  const stored = '/api/site-public/files/images/abc';
  const api = 'https://api.example';

  it('names the signed-in tenant on a stored address, and only there', () => {
    expect(absoluteFileUrl(stored, api, 't1')).toBe(`${api}${stored}?__tenant=t1`);
    expect(absoluteFileUrl(stored, '', 't1')).toBe(`${stored}?__tenant=t1`);
    expect(absoluteFileUrl(`${stored}?Width=80`, api, 't1')).toBe(`${api}${stored}?Width=80&__tenant=t1`);
    expect(absoluteFileUrl(stored, api, null)).toBe(`${api}${stored}`);
    expect(absoluteFileUrl('https://cdn.example/a.png', api, 't1')).toBe('https://cdn.example/a.png');
  });

  it('keeps the tenant an address already names', () => {
    expect(absoluteFileUrl(`${stored}?__tenant=`, api, 't1')).toBe(`${api}${stored}?__tenant=`);
    expect(absoluteFileUrl(`${stored}?__tenant=t2`, api, 't1')).toBe(`${api}${stored}?__tenant=t2`);
  });

  it('takes the host and the signed-in tenant off again', () => {
    expect(relativeFileUrl(`${api}${stored}?__tenant=t1`, api, 't1')).toBe(stored);
    expect(relativeFileUrl(`${api}${stored}?Width=80&__tenant=t1`, api, 't1')).toBe(`${stored}?Width=80`);
    expect(relativeFileUrl(`${stored}?__tenant=t1`, '', 't1')).toBe(stored);
    expect(relativeFileUrl(`${api}${stored}`, api, null)).toBe(stored);
  });

  it('leaves another tenant, an empty tenant and other addresses alone', () => {
    expect(relativeFileUrl(`${api}${stored}?__tenant=t2`, api, 't1')).toBe(`${stored}?__tenant=t2`);
    expect(relativeFileUrl(`${api}${stored}?__tenant=`, api, 't1')).toBe(`${stored}?__tenant=`);
    expect(relativeFileUrl('https://cdn.example/a.png?__tenant=t1', api, 't1')).toBe('https://cdn.example/a.png?__tenant=t1');
  });
});
