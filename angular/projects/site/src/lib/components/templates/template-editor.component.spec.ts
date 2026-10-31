import { PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import { FieldAdminService } from '../../proxy/dignite/site/admin/fields/field-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import type {
  SiteTemplatePageUsageDto,
  SiteTemplateUsageDto,
} from '../../proxy/dignite/site/admin/templates/models';
import { SiteTemplateAdminService } from '../../proxy/dignite/site/admin/templates/site-template-admin.service';
import type { ContentDto } from '../../proxy/dignite/site/contents/models';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import type {
  SiteTemplateDto,
  SiteTemplatePreviewLinkDto,
  SiteTemplateVersionDto,
} from '../../proxy/dignite/site/templates/models';
import type { CodeEditorComponent } from './code-editor.component';
import {
  TemplateEditorComponent,
  parseSyntaxErrors,
  skeletonFor,
  snippetFor,
} from './template-editor.component';

describe('TemplateEditorComponent', () => {
  const template = (overrides: Partial<SiteTemplateDto> = {}) =>
    ({ id: 't1', name: 'Default', publishedVersionNumber: 1, latestVersionNumber: 2, ...overrides }) as SiteTemplateDto;

  const version = (number: number, body = `<p>v${number}</p>`) =>
    ({ id: `v${number}`, templateId: 't1', number, body, comment: `comment ${number}` }) as SiteTemplateVersionDto;

  const aboutPage = {
    pageId: 'p1',
    name: 'about',
    displayName: 'About',
    route: 'about',
    template: 'Default',
    hasSlug: false,
    path: '/about',
    sampleContent: null,
  } as SiteTemplatePageUsageDto;

  const slugPage = {
    pageId: 'p2',
    name: 'blog',
    displayName: 'Blog',
    route: 'blog/{slug}',
    template: 'Default',
    hasSlug: true,
    path: '/blog',
    sampleContent: { contentId: 'c2', slug: 'two', cultureName: 'en', path: '/blog/two' },
  } as SiteTemplatePageUsageDto;

  const slugPageFirst = [slugPage, aboutPage];

  const usage = (pages: SiteTemplatePageUsageDto[] = [aboutPage]) =>
    ({ templateId: 't1', templateName: 'Default', kind: 0, pages, referencedBy: [] }) as SiteTemplateUsageDto;

  const content = (id: string, slug: string) =>
    ({ id, pageId: 'p2', slug, cultureName: 'en', url: `/blog/${slug}` }) as ContentDto;

  const link = {
    url: 'https://site.example/blog/two?site-preview=token',
    path: '/blog/two',
    templateName: 'Default',
    versionNumber: 2,
  } as SiteTemplatePreviewLinkDto;

  /** An ABP error response: the HTTP error's body is `{ error: { code, message } }`. */
  const apiError = (code: string, message: string) => ({ error: { error: { code, message } } });

  const field = (id: string, name: string, displayName: string, fieldTypeName: string) =>
    ({ id, name, displayName, fieldTypeName }) as FieldDto;

  const fieldLibrary = [
    field('f-title', 'title', 'Title', 'Text'),
    field('f-body', 'body', 'Body', 'CKEditor'),
    field('f-published', 'published-on', 'Published on', 'DateTime'),
    field('f-seo', 'seo', 'SEO', 'Seo'),
  ];

  /** The blog page: its first content type lists the fields out of order, with the SEO block among them. */
  const blogPageDto = {
    id: 'p2',
    name: 'blog',
    displayName: 'Blog',
    contentTypes: [
      {
        id: 'ct1',
        name: 'post',
        displayName: 'Post',
        fields: [
          { fieldId: 'f-body', order: 2 },
          { fieldId: 'f-seo', order: 3 },
          { fieldId: 'f-title', order: 1 },
        ],
      },
      { id: 'ct2', name: 'note', displayName: 'Note', fields: [{ fieldId: 'f-published', order: 1 }] },
      { id: 'ct3', name: 'empty', displayName: 'Empty', fields: [] },
    ],
  } as PageDto;

  function create(
    overrides: {
      id?: string | null;
      /** The query string of the address, for create mode. */
      query?: Record<string, string>;
      templateService?: object;
      pageService?: object;
      fieldService?: object;
      contents?: ContentDto[];
      toaster?: object;
      router?: object;
    } = {},
  ) {
    const id = overrides.id === undefined ? 't1' : overrides.id;
    const success = vi.fn();
    const navigate = vi.fn();

    // The window the preview opens: its `location` is assigned to, `close` is called when no link comes.
    const win = { location: { href: '' }, close: vi.fn(), opener: {} as object | null };
    const open = vi.fn(() => win);
    const getContents = vi.fn(() =>
      of({ items: overrides.contents ?? [content('c1', 'one'), content('c2', 'two')], totalCount: 2 }),
    );
    const getPreviewLink = vi.fn(() => of(link));
    const getPage = vi.fn((pageId: string, _config?: object) =>
      of(pageId === 'p2' ? blogPageDto : ({ id: pageId, name: 'about', displayName: 'About', contentTypes: [] } as PageDto)),
    );
    const findPageByName = vi.fn((_name: string, _config?: object) => of(blogPageDto));
    const getFields = vi.fn((_input: object, _config?: object) => of({ items: fieldLibrary }));

    TestBed.configureTestingModule({
      providers: [
        TemplateEditorComponent,
        {
          provide: SiteTemplateAdminService,
          useValue: {
            get: vi.fn(() => of(template())),
            getVersions: vi.fn(() => of({ items: [version(1), version(2)] })),
            getVersion: vi.fn((_id: string, number: number) => of(version(number))),
            getUsage: vi.fn(() => of(usage())),
            getPreviewLink,
            ...overrides.templateService,
          },
        },
        { provide: ContentAdminService, useValue: { getList: getContents } },
        {
          provide: PageAdminService,
          useValue: { get: getPage, findByName: findPageByName, ...overrides.pageService },
        },
        { provide: FieldAdminService, useValue: { getList: getFields, ...overrides.fieldService } },
        { provide: DOCUMENT, useValue: { defaultView: { open } } },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        { provide: ToasterService, useValue: overrides.toaster ?? { success } },
        { provide: Router, useValue: overrides.router ?? { navigate } },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap(id ? { id } : {}),
              queryParamMap: convertToParamMap(overrides.query ?? {}),
            },
          },
        },
      ],
    });

    return {
      component: TestBed.inject(TemplateEditorComponent),
      success,
      navigate,
      open,
      win,
      getContents,
      getPreviewLink,
      getPage,
      findPageByName,
      getFields,
    };
  }

  describe('opening an existing template', () => {
    it('starts on the published version, with the versions newest first', () => {
      const { component } = create();

      expect(component.isLoading()).toBe(false);
      expect(component.body()).toBe('<p>v1</p>');
      expect(component.selectedVersion()).toBe(1);
      expect(component.versions().map(v => v.number)).toEqual([2, 1]);
    });

    it('starts on the latest version when nothing is published', () => {
      const get = vi.fn(() => of(template({ publishedVersionNumber: undefined })));
      const { component } = create({ templateService: { get } });

      expect(component.body()).toBe('<p>v2</p>');
      expect(component.selectedVersion()).toBe(2);
    });

    it('loads an older version into the editor on request', () => {
      const { component } = create();

      component.loadVersion(2);

      expect(component.body()).toBe('<p>v2</p>');
      expect(component.loadedVersion()).toBe(2);
    });
  });

  describe('save', () => {
    it('saves a draft as a new version with publish: false, and selects it for the preview without opening a window', () => {
      const update = vi.fn(() => of(template({ latestVersionNumber: 3 })));
      const { component, success, open, getPreviewLink } = create({ templateService: { update } });

      component.body.set('<p>edited</p>');
      component.form.patchValue({ comment: 'fix typo' });
      component.save(false);

      expect(update).toHaveBeenCalledWith(
        't1',
        { body: '<p>edited</p>', comment: 'fix typo', publish: false },
        { skipHandleError: true },
      );
      expect(success).toHaveBeenCalledWith('Site::TemplateSaved', undefined, {
        messageLocalizationParams: ['3'],
      });
      expect(component.selectedVersion()).toBe(3);
      expect(open).not.toHaveBeenCalled();
      expect(getPreviewLink).not.toHaveBeenCalled();
      expect(component.isBusy()).toBe(false);
    });

    it('passes publish: true for save and publish', () => {
      const update = vi.fn(() => of(template({ latestVersionNumber: 3, publishedVersionNumber: 3 })));
      const { component, success } = create({ templateService: { update } });

      component.body.set('<p>edited</p>');
      component.save(true);

      expect(update).toHaveBeenCalledWith(
        't1',
        { body: '<p>edited</p>', comment: undefined, publish: true },
        { skipHandleError: true },
      );
      expect(success).toHaveBeenCalledWith('Site::TemplatePublished', undefined, {
        messageLocalizationParams: ['3'],
      });
    });

    it('fills the syntax error list from a Site:090002 error and does not toast', () => {
      const update = vi.fn(() =>
        throwError(() =>
          apiError(
            'Site:090002',
            "The template 'Default' has syntax errors:\n(3,12): Unexpected token '}'\n(7,1): Missing end",
          ),
        ),
      );
      const { component, success } = create({ templateService: { update } });

      component.save(false);

      expect(component.syntaxErrors()).toEqual([
        { line: 3, column: 12, message: "Unexpected token '}'" },
        { line: 7, column: 1, message: 'Missing end' },
      ]);
      expect(component.saveError()).toBeNull();
      expect(success).not.toHaveBeenCalled();
      expect(component.isBusy()).toBe(false);
    });

    it('shows any other error as a message, and clears the syntax errors', () => {
      const update = vi
        .fn()
        .mockReturnValueOnce(throwError(() => apiError('Site:090002', 'headline\n(1,1): oops')))
        .mockReturnValueOnce(
          throwError(() => apiError('Site:090004', 'The layout template must call site_head.')),
        );
      const { component } = create({ templateService: { update } });

      component.save(false);
      expect(component.syntaxErrors()).toHaveLength(1);

      component.save(false);
      expect(component.syntaxErrors()).toEqual([]);
      expect(component.saveError()).toBe('The layout template must call site_head.');
    });

    it('creates a template under its name and opens it for editing', () => {
      const createTemplate = vi.fn(() => of(template({ id: 'new-id', name: 'Blog/Article', latestVersionNumber: 1 })));
      const { component, navigate } = create({ id: null, templateService: { create: createTemplate } });

      component.form.patchValue({ name: 'Blog/Article' });
      component.body.set('<p>hello</p>');
      component.save(false);

      expect(createTemplate).toHaveBeenCalledWith(
        { name: 'Blog/Article', body: '<p>hello</p>', comment: undefined, publish: false },
        { skipHandleError: true },
      );
      expect(navigate).toHaveBeenCalledWith(['/site/templates', 'new-id', 'edit']);
    });

    it('does not save a name the server would reject', () => {
      const createTemplate = vi.fn();
      const { component } = create({ id: null, templateService: { create: createTemplate } });

      component.form.patchValue({ name: 'not valid!' });
      component.save(false);

      expect(createTemplate).not.toHaveBeenCalled();
    });
  });

  describe('publishVersion', () => {
    it('publishes the version, reloads the template and versions, and toasts', () => {
      const publish = vi.fn(() => of(template({ publishedVersionNumber: 2 })));
      const get = vi.fn(() => of(template({ publishedVersionNumber: 2 })));
      const getVersions = vi.fn(() => of({ items: [version(1), version(2)] }));
      const { component, success } = create({ templateService: { publish, get, getVersions } });
      get.mockClear();
      getVersions.mockClear();

      component.publishVersion(2);

      expect(publish).toHaveBeenCalledWith('t1', 2);
      expect(get).toHaveBeenCalledWith('t1');
      expect(getVersions).toHaveBeenCalledWith('t1');
      expect(component.template()?.publishedVersionNumber).toBe(2);
      expect(success).toHaveBeenCalledWith('Site::TemplatePublished', undefined, {
        messageLocalizationParams: ['2'],
      });
      expect(component.isBusy()).toBe(false);
    });

    it('reloads the usage with the template and versions', () => {
      const getUsage = vi.fn(() => of(usage()));
      const publish = vi.fn(() => of(template()));
      const { component } = create({ templateService: { getUsage, publish } });
      getUsage.mockClear();

      component.publishVersion(2);

      expect(getUsage).toHaveBeenCalledWith('t1');
    });

    it('keeps the chosen page when the usage is reloaded', () => {
      const publish = vi.fn(() => of(template()));
      const { component } = create({
        templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))), publish },
      });

      component.selectPage('p1');
      component.publishVersion(2);

      expect(component.selectedPageId()).toBe('p1');
    });
  });

  describe('the page and content the preview opens', () => {
    it('selects the first page of the usage, and loads no contents for a page without a slug', () => {
      const { component, getContents } = create();

      expect(component.usage()?.templateName).toBe('Default');
      expect(component.selectedPageId()).toBe('p1');
      expect(component.selectedPage()?.displayName).toBe('About');
      expect(getContents).not.toHaveBeenCalled();
      expect(component.contents()).toEqual([]);
      expect(component.selectedContentId()).toBeUndefined();
    });

    it('loads the published contents of a slug page, newest first, and defaults to its sample content', () => {
      const { component, getContents } = create({
        templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))) },
      });

      expect(getContents).toHaveBeenCalledWith({
        pageId: 'p2',
        status: 1,
        maxResultCount: 100,
        sorting: 'publishTime desc',
      });
      expect(component.contents().map(content => content.id)).toEqual(['c1', 'c2']);
      expect(component.selectedContentId()).toBe('c2');
    });

    it('falls back to the first content when the sample content is not in the list', () => {
      const sample = { contentId: 'gone', slug: 'gone', cultureName: 'en', path: '/blog/gone' };
      const { component } = create({
        templateService: {
          getUsage: vi.fn(() => of(usage([{ ...slugPage, sampleContent: sample }, aboutPage]))),
        },
      });

      expect(component.selectedContentId()).toBe('c1');
    });

    it('has no selection when the template is used by no page', () => {
      const { component, getContents } = create({
        templateService: { getUsage: vi.fn(() => of(usage([]))) },
      });

      expect(component.selectedPageId()).toBeUndefined();
      expect(component.previewPath()).toBe('/');
      expect(getContents).not.toHaveBeenCalled();
    });

    it('clears and reloads the contents when another page is chosen', () => {
      const { component, getContents } = create({
        templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))) },
      });

      component.selectPage('p1');
      expect(component.contents()).toEqual([]);
      expect(component.selectedContentId()).toBeUndefined();

      component.selectPage('p2');
      expect(getContents).toHaveBeenCalledTimes(2);
      expect(component.selectedContentId()).toBe('c2');
    });
  });

  describe('previewPath', () => {
    it('is the page path when no content applies', () => {
      const { component } = create();

      expect(component.previewPath()).toBe('/about');
    });

    it('is the selected content url on a slug page', () => {
      const { component } = create({ templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))) } });

      expect(component.previewPath()).toBe('/blog/two');

      component.selectedContentId.set('c1');
      expect(component.previewPath()).toBe('/blog/one');
    });

    it('is the page path when the slug page has no content', () => {
      const { component } = create({
        templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))) },
        contents: [],
      });

      expect(component.previewPath()).toBe('/blog');
    });

    it('prefers the typed path to anything chosen, ignoring blanks', () => {
      const { component } = create({ templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))) } });

      component.customPath.set('  /ja/special  ');
      expect(component.previewPath()).toBe('/ja/special');

      component.customPath.set('   ');
      expect(component.previewPath()).toBe('/blog/two');
    });
  });

  describe('openPreview', () => {
    it('opens the window first, then points it at the link and selects the version', () => {
      const { component, open, win, getPreviewLink } = create();
      let openedBeforeRequest = false;
      getPreviewLink.mockImplementationOnce(() => {
        openedBeforeRequest = open.mock.calls.length === 1;
        return of(link);
      });

      component.openPreview(2);

      expect(open).toHaveBeenCalledWith('', '_blank');
      expect(openedBeforeRequest).toBe(true);
      expect(getPreviewLink).toHaveBeenCalledWith('t1', 2, '/about', { skipHandleError: true });
      expect(win.location.href).toBe(link.url);
      expect(win.opener).toBeNull();
      expect(win.close).not.toHaveBeenCalled();
      expect(component.selectedVersion()).toBe(2);
      expect(component.previewLinkError()).toBeNull();
    });

    it('previews the selected version, with the chosen content, when called without one', () => {
      const { component, getPreviewLink } = create({
        templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))) },
      });

      component.openPreview();

      expect(getPreviewLink).toHaveBeenCalledWith('t1', 1, '/blog/two', { skipHandleError: true });
    });

    it('closes the window and shows the server message when no link can be issued', () => {
      const getPreviewLink = vi.fn(() =>
        throwError(() => apiError('Site:090009', 'Previewing needs the public site in this application.')),
      );
      const { component, win } = create({ templateService: { getPreviewLink } });

      component.openPreview(2);

      expect(win.close).toHaveBeenCalledTimes(1);
      expect(win.location.href).toBe('');
      expect(component.previewLinkError()).toBe('Previewing needs the public site in this application.');
    });

    it('clears an earlier error when it tries again', () => {
      const getPreviewLink = vi
        .fn()
        .mockReturnValueOnce(throwError(() => apiError('Site:090009', 'nope')))
        .mockReturnValueOnce(of(link));
      const { component } = create({ templateService: { getPreviewLink } });

      component.openPreview();
      expect(component.previewLinkError()).toBe('nope');

      component.openPreview();
      expect(component.previewLinkError()).toBeNull();
    });

    it('asks for nothing while no version is selected', () => {
      const { component, open, getPreviewLink } = create();

      component.selectedVersion.set(undefined);
      component.openPreview();

      expect(open).not.toHaveBeenCalled();
      expect(getPreviewLink).not.toHaveBeenCalled();
    });
  });

  describe('creating from the address', () => {
    it('starts blank when the address names nothing', () => {
      const { component, findPageByName, getFields } = create({ id: null });

      expect(component.form.get('name')?.value).toBe('');
      expect(component.body()).toBe('');
      expect(component.forPage()).toBeUndefined();
      expect(component.showFieldsColumn()).toBe(false);
      expect(findPageByName).not.toHaveBeenCalled();
      expect(getFields).not.toHaveBeenCalled();
    });

    it('prefills the name and the page skeleton, and loads the fields of the page', () => {
      const { component, findPageByName, getFields } = create({
        id: null,
        query: { name: 'Blog/Post', page: 'blog' },
      });

      expect(component.form.get('name')?.value).toBe('Blog/Post');
      expect(component.body()).toBe(skeletonFor('Blog/Post'));
      expect(findPageByName).toHaveBeenCalledWith('blog', { skipHandleError: true });
      expect(component.forPage()?.displayName).toBe('Blog');
      expect(component.showFieldsColumn()).toBe(true);
      expect(getFields).toHaveBeenCalledTimes(1);
      expect(component.fieldGroups().map(group => group.displayName)).toEqual(['Post', 'Note']);
    });

    it('stores the name the way the server does: trimmed, without a leading slash', () => {
      const { component } = create({ id: null, query: { name: ' /Blog/Post ' } });

      expect(component.form.get('name')?.value).toBe('Blog/Post');
    });

    it('starts a partial from the partial skeleton', () => {
      const { component } = create({ id: null, query: { name: 'partials/card' } });

      expect(component.form.get('name')?.value).toBe('partials/card');
      expect(component.body()).toBe(skeletonFor('partials/card'));
    });

    it('keeps the form usable, with no hint and no sidebar, when the page is gone', () => {
      const { component } = create({
        id: null,
        query: { name: 'Blog/Post', page: 'gone' },
        pageService: { findByName: vi.fn(() => throwError(() => new Error('not found'))) },
      });

      expect(component.forPage()).toBeUndefined();
      expect(component.showFieldsColumn()).toBe(false);
      expect(component.fieldGroups()).toEqual([]);
      expect(component.body()).toBe(skeletonFor('Blog/Post'));
    });

    it('does not prefill anything when editing an existing template', () => {
      const { component, findPageByName } = create({ query: { name: 'Other', page: 'blog' } });

      expect(findPageByName).not.toHaveBeenCalled();
      expect(component.forPage()).toBeUndefined();
      expect(component.body()).toBe('<p>v1</p>');
    });
  });

  describe('the field sidebar', () => {
    it('lists nothing while the template is used by no page', () => {
      const { component, getPage, getFields } = create({
        templateService: { getUsage: vi.fn(() => of(usage([]))) },
      });

      expect(component.sidebarPage()).toBeUndefined();
      expect(component.fieldGroups()).toEqual([]);
      expect(getPage).not.toHaveBeenCalled();
      expect(getFields).not.toHaveBeenCalled();
    });

    it('lists the fields of the page selected for the preview, grouped by content type', () => {
      const { component, getPage } = create({
        templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))) },
      });

      expect(getPage).toHaveBeenCalledWith('p2', { skipHandleError: true });
      expect(component.fieldGroups()).toEqual([
        {
          id: 'ct1',
          displayName: 'Post',
          // In arrangement order, and without the SEO block.
          fields: [
            { name: 'title', displayName: 'Title', fieldTypeName: 'Text', snippet: '{{ content.fields.title }}' },
            { name: 'body', displayName: 'Body', fieldTypeName: 'CKEditor', snippet: '{{ render_field "body" }}' },
          ],
        },
        {
          id: 'ct2',
          displayName: 'Note',
          fields: [
            {
              name: 'published-on',
              displayName: 'Published on',
              fieldTypeName: 'DateTime',
              snippet: '{{ content.fields["published-on"] | date.to_string "%Y-%m-%d" }}',
            },
          ],
        },
      ]);
    });

    it('follows the page chosen for the preview, reading the field library only once', () => {
      const { component, getPage, getFields } = create({
        templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))) },
      });

      component.selectPage('p1');
      expect(getPage).toHaveBeenLastCalledWith('p1', { skipHandleError: true });
      expect(component.fieldGroups()).toEqual([]);

      component.selectPage('p2');
      expect(component.fieldGroups().length).toBe(2);
      expect(getFields).toHaveBeenCalledTimes(1);
      expect(getFields).toHaveBeenCalledWith({}, { skipHandleError: true });
    });

    it('lists nothing when the field library cannot be read', () => {
      const { component } = create({
        templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))) },
        fieldService: { getList: vi.fn(() => throwError(() => new Error('forbidden'))) },
      });

      expect(component.sidebarPage()?.displayName).toBe('Blog');
      expect(component.fieldGroups()).toEqual([]);
    });

    it('inserts the snippet of a clicked field through the code editor', () => {
      const insert = vi.fn();
      const { component } = create({
        templateService: { getUsage: vi.fn(() => of(usage(slugPageFirst))) },
      });
      component.codeEditor = { insert } as unknown as CodeEditorComponent;

      component.insertField(component.fieldGroups()[0].fields[1]);

      expect(insert).toHaveBeenCalledWith('{{ render_field "body" }}');
    });
  });

  describe('referencedByNames', () => {
    it('lists each referring template once', () => {
      const reference = (templateName: string) => ({ templateId: templateName, templateName });
      const { component } = create({
        templateService: {
          getUsage: vi.fn(() =>
            of({ ...usage(), kind: 2, referencedBy: [reference('layout'), reference('Blog/Post'), reference('layout')] }),
          ),
        },
      });

      expect(component.referencedByNames()).toBe('layout, Blog/Post');
    });
  });
});

describe('skeletonFor', () => {
  it('gives the layout the document shell with site_head and the body', () => {
    const skeleton = skeletonFor('layout');

    expect(skeleton).toContain('<!DOCTYPE html>');
    expect(skeleton).toContain('{{ site_head }}');
    expect(skeleton).toContain('{{ body }}');
    expect(skeletonFor('/layout')).toBe(skeleton);
  });

  it('gives a partial a wrapper with a placeholder', () => {
    expect(skeletonFor('partials/post-card')).toBe('<div>\n    {{ $1 }}\n</div>\n');
  });

  it('gives an asset nothing, since it is not Scriban', () => {
    expect(skeletonFor('assets/site.css')).toBe('');
  });

  it('gives anything else the page skeleton', () => {
    for (const name of ['Default', 'Blog/Post', '', 'Layout', 'partialsx/a']) {
      const skeleton = skeletonFor(name);

      expect(skeleton).toContain('<h1>{{ page.display_name }}</h1>');
      expect(skeleton).toContain('{{ render_field field.name }}');
      expect(skeleton).toContain('{{ list = contents { max: 20 } }}');
    }
  });
});

describe('snippetFor', () => {
  it('prints plain values straight off the content fields', () => {
    for (const type of ['Text', 'Number', 'Boolean', 'Select', 'Tree']) {
      expect(snippetFor(type, 'title')).toBe('{{ content.fields.title }}');
    }
  });

  it('formats a date', () => {
    expect(snippetFor('DateTime', 'published_on')).toBe(
      '{{ content.fields.published_on | date.to_string "%Y-%m-%d" }}',
    );
  });

  it('renders everything composite or unknown with render_field', () => {
    for (const type of ['CKEditor', 'Matrix', 'Table', 'Content', 'FileExplorer', 'Whatever']) {
      expect(snippetFor(type, 'body')).toBe('{{ render_field "body" }}');
    }
  });

  it('reads a name that is not a plain identifier with the indexer', () => {
    expect(snippetFor('Text', 'cover-title')).toBe('{{ content.fields["cover-title"] }}');
    expect(snippetFor('Text', '2nd')).toBe('{{ content.fields["2nd"] }}');
  });
});

describe('parseSyntaxErrors', () => {
  it('reads one entry per (line,column): message line and skips the headline', () => {
    expect(parseSyntaxErrors("The template 'X' has syntax errors:\r\n(3,12): a\r\n(7,1): b: c")).toEqual([
      { line: 3, column: 12, message: 'a' },
      { line: 7, column: 1, message: 'b: c' },
    ]);
  });

  it('returns nothing for a message with no entries', () => {
    expect(parseSyntaxErrors('something else')).toEqual([]);
    expect(parseSyntaxErrors(undefined)).toEqual([]);
  });
});
