import { SiteThemeFileStatus } from '../../proxy/dignite/site/themes/site-theme-file-status.enum';
import {
  buildThemeTree,
  errorMessageOf,
  extensionOf,
  fieldGroupsFor,
  foldersOf,
  isAssetPath,
  languageOf,
  normalizeThemePath,
  pageTemplatePath,
  snippetFor,
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

describe('snippetFor', () => {
  it('reads plain values off content.fields, formats a date and renders anything else', () => {
    expect(snippetFor('Text', 'title')).toBe('{{ content.fields.title }}');
    expect(snippetFor('Boolean', 'featured')).toBe('{{ content.fields.featured }}');
    expect(snippetFor('DateTime', 'starts')).toBe('{{ content.fields.starts | date.to_string "%Y-%m-%d" }}');
    expect(snippetFor('CKEditor', 'body')).toBe('{{ render_field "body" }}');
    expect(snippetFor('Matrix', 'blocks')).toBe('{{ render_field "blocks" }}');
  });

  it('reads a name that is not an identifier with the indexer', () => {
    expect(snippetFor('Text', 'sub-title')).toBe('{{ content.fields["sub-title"] }}');
  });
});

describe('fieldGroupsFor', () => {
  const library = [
    { id: 'f-title', name: 'title', displayName: 'Title', fieldTypeName: 'Text' },
    { id: 'f-body', name: 'body', displayName: 'Body', fieldTypeName: 'CKEditor' },
    { id: 'f-seo', name: 'seo', displayName: 'SEO', fieldTypeName: 'Seo' },
  ];

  it('lists each content type with its fields in order, named as arranged, without the SEO block', () => {
    const page = {
      contentTypes: [
        {
          id: 'ct-event',
          displayName: 'Event',
          fields: [
            { fieldId: 'f-body', order: 2 },
            { fieldId: 'f-title', order: 1, displayName: 'Event name' },
            { fieldId: 'f-seo', order: 3 },
          ],
        },
        { id: 'ct-empty', displayName: 'Only SEO', fields: [{ fieldId: 'f-seo', order: 1 }] },
      ],
    };

    expect(fieldGroupsFor(page, library)).toEqual([
      {
        id: 'ct-event',
        displayName: 'Event',
        fields: [
          { name: 'title', displayName: 'Event name', fieldTypeName: 'Text', snippet: '{{ content.fields.title }}' },
          { name: 'body', displayName: 'Body', fieldTypeName: 'CKEditor', snippet: '{{ render_field "body" }}' },
        ],
      },
    ]);
  });

  it('is empty until both the page and the library are there', () => {
    expect(fieldGroupsFor(undefined, library)).toEqual([]);
    expect(fieldGroupsFor({ contentTypes: [] }, undefined)).toEqual([]);
  });
});
