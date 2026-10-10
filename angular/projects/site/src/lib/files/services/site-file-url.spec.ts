import { isSiteFileUrl, sizedFileUrl } from './site-file-url';

describe('sizedFileUrl', () => {
  const file = 'https://api.example/api/site-public/files/site-images/abc?__tenant=';

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
