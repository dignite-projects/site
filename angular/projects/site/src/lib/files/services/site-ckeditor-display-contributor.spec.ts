import { TestBed } from '@angular/core/testing';
import { EnvironmentService } from '@abp/ng.core';
import type { CKEditorDisplayContext } from '@dignite/ng.flex-fields-ckeditor';
import { siteCKEditorDisplayContributor } from './site-ckeditor-display-contributor';

describe('siteCKEditorDisplayContributor', () => {
  const API = 'https://api.example';
  const PATH = '/api/site-public/files/site-images/abc.png?__tenant=';
  const apis = { default: { url: 'https://default.example' }, SiteAdmin: { url: `${API}/` } };

  function display(html: string, withApis = true): string {
    if (withApis) {
      TestBed.inject(EnvironmentService).setState({ apis } as never);
    }

    const contributor = TestBed.runInInjectionContext(() => siteCKEditorDisplayContributor());
    return contributor(html, {} as CKEditorDisplayContext);
  }

  it('returns the HTML as it is when the UI shares its API origin', () => {
    const html = `<p><img src="${PATH}"></p>`;

    expect(display(html, false)).toBe(html);
  });

  it('puts the API host in front of a double-quoted src', () => {
    expect(display(`<p><img src="${PATH}" alt="a"></p>`)).toBe(`<p><img src="${API}${PATH}" alt="a"></p>`);
  });

  it('puts the API host in front of a single-quoted src', () => {
    expect(display(`<img alt='a' src='${PATH}'>`)).toBe(`<img alt='a' src='${API}${PATH}'>`);
  });

  it('rewrites every image in the HTML', () => {
    expect(display(`<img src="${PATH}"><img src='${PATH}'>`)).toBe(`<img src="${API}${PATH}"><img src='${API}${PATH}'>`);
  });

  it('leaves external addresses and href alone', () => {
    const html = `<img src="https://cdn.example${PATH}"><img src="/other/a.png"><a href="${PATH}">file</a>`;

    expect(display(html)).toBe(html);
  });

  it('leaves an address that is already absolute alone', () => {
    const html = `<img src="${API}${PATH}">`;

    expect(display(html)).toBe(html);
  });
});
