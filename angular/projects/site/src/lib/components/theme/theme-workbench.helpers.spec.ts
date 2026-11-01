import { SiteThemeFileStatus } from '../../proxy/dignite/site/themes/site-theme-file-status.enum';
import {
  buildThemeTree,
  errorMessageOf,
  extensionOf,
  foldersOf,
  isAssetPath,
  languageOf,
  normalizeThemePath,
  pageTemplatePath,
  routeToPreviewPath,
  skeletonFor,
  themePathProblem,
} from './theme-workbench.helpers';

describe('theme workbench helpers', () => {
  describe('paths', () => {
    it('normalizes like the server', () => {
      expect(normalizeThemePath(' /blog/post ')).toBe('blog/post');
    });

    it('tells a static file from a template by the extension of the last segment', () => {
      expect(extensionOf('assets/site.css')).toBe('css');
      expect(extensionOf('js/menu.min.js')).toBe('js');
      expect(extensionOf('layouts/default')).toBeNull();
      expect(isAssetPath('assets/site.css')).toBe(true);
      expect(isAssetPath('_card')).toBe(false);
    });

    it.each([
      ['Events', null],
      ['blog/_card', null],
      ['assets/site.css', null],
      ['', 'Site::ThemePathInvalid'],
      ['a//b', 'Site::ThemePathInvalid'],
      ['../x', 'Site::ThemePathInvalid'],
      ['dir.v2/file', 'Site::ThemePathInvalid'],
      ['post.html', 'Site::ThemePathExtensionNotAllowed'],
      ['image.png', 'Site::ThemePathExtensionNotAllowed'],
    ])('checks %s', (path, problem) => {
      expect(themePathProblem(path)).toBe(problem);
    });

    it('lists the folders on the way to a file', () => {
      expect(foldersOf('blog/archive/post')).toEqual(['blog', 'blog/archive']);
      expect(foldersOf('Events')).toEqual([]);
    });
  });

  describe('editor language', () => {
    it.each([
      ['Events', 'html'],
      ['icons/logo.svg', 'html'],
      ['assets/site.css', 'css'],
      ['js/site.js', 'javascript'],
      ['data/feed.json', 'javascript'],
      ['robots.txt', 'text'],
    ])('highlights %s as %s', (path, language) => {
      expect(languageOf(path)).toBe(language);
    });
  });

  describe('skeletons', () => {
    it('starts a layout with site_head and body', () => {
      const body = skeletonFor('layouts/landing');
      expect(body).toContain('{{ site_head }}');
      expect(body).toContain('{{ body }}');
    });

    it('starts a partial, a page template and a static file differently', () => {
      expect(skeletonFor('blog/_card')).not.toContain('site_head');
      expect(skeletonFor('blog/post')).toContain('page.display_name');
      expect(skeletonFor('assets/extra.css')).toBe('');
    });
  });

  describe('tree', () => {
    const tree = buildThemeTree([
      { path: 'layouts/default', isAsset: false, status: SiteThemeFileStatus.Unchanged },
      { path: 'Events', isAsset: false, status: SiteThemeFileStatus.Modified },
      { path: 'assets/site.css', isAsset: true, status: SiteThemeFileStatus.Unchanged },
      { path: 'blog/_card', isAsset: false, status: SiteThemeFileStatus.Added },
      { path: 'blog/post', isAsset: false, status: SiteThemeFileStatus.Unchanged },
      { path: '_footer', isAsset: false, status: SiteThemeFileStatus.Unchanged },
    ]);

    it('puts folders first, then files, each by name', () => {
      expect(tree.map(node => node.name)).toEqual(['assets', 'blog', 'layouts', 'Events', '_footer']);
      expect(tree[1].children.map(node => node.path)).toEqual(['blog/_card', 'blog/post']);
    });

    it('marks a folder with a change in it', () => {
      const byName = Object.fromEntries(tree.map(node => [node.name, node]));
      expect(byName['blog'].hasChanges).toBe(true);
      expect(byName['layouts'].hasChanges).toBe(false);
      expect(byName['Events'].hasChanges).toBe(true);
    });
  });

  it("reads an ABP error's message with its validation details", () => {
    expect(
      errorMessageOf({
        error: { error: { message: 'Invalid', validationErrors: [{ message: 'Path is required.' }] } },
      }),
    ).toBe('Invalid\nPath is required.');
  });
});

describe('pageTemplatePath', () => {
  it('drops a trailing .cshtml and a leading slash, as the site resolves a page template', () => {
    expect(pageTemplatePath('Events')).toBe('Events');
    expect(pageTemplatePath('Default.cshtml')).toBe('Default');
    expect(pageTemplatePath(' /blog/Article.CSHTML ')).toBe('blog/Article');
  });
});

describe('routeToPreviewPath', () => {
  it('keeps the literal segments up to an optional parameter', () => {
    expect(routeToPreviewPath('/')).toBe('/');
    expect(routeToPreviewPath('/about')).toBe('/about');
    expect(routeToPreviewPath('/events/{slug?}')).toBe('/events');
    expect(routeToPreviewPath('/news/{news-category?:^(news|tutorials)$}/{publishTime?:yyyy:^\\d{4}$}')).toBe('/news');
    expect(routeToPreviewPath('/docs/{page=intro}')).toBe('/docs');
    expect(routeToPreviewPath('/files/{**rest}')).toBe('/files');
  });

  it('has no address for a route whose first parameter is required', () => {
    expect(routeToPreviewPath('/news/{publishTime:yyyy:^\\d{4}$}/{slug}')).toBeNull();
    expect(routeToPreviewPath('/blog/{slug}')).toBeNull();
    expect(routeToPreviewPath('/page-{n}')).toBeNull();
  });
});
