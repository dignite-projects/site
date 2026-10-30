import { EnvironmentService, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { SiteTemplateAdminService } from '../../proxy/dignite/site/admin/templates/site-template-admin.service';
import type {
  SiteTemplateDto,
  SiteTemplatePreviewDto,
  SiteTemplateVersionDto,
} from '../../proxy/dignite/site/templates/models';
import {
  TemplateEditorComponent,
  parseSyntaxErrors,
  withBaseHref,
} from './template-editor.component';

const API_URL = 'https://api.example';

describe('TemplateEditorComponent', () => {
  const template = (overrides: Partial<SiteTemplateDto> = {}) =>
    ({ id: 't1', name: 'Default', publishedVersionNumber: 1, latestVersionNumber: 2, ...overrides }) as SiteTemplateDto;

  const version = (number: number, body = `<p>v${number}</p>`) =>
    ({ id: `v${number}`, templateId: 't1', number, body, comment: `comment ${number}` }) as SiteTemplateVersionDto;

  const previewResult = (overrides: Partial<SiteTemplatePreviewDto> = {}) =>
    ({
      html: '<!doctype html><html><head><title>x</title></head><body>hi</body></html>',
      path: '/about',
      pageName: 'about',
      cultureName: 'en',
      pageTemplate: 'Default',
      versionNumber: 2,
      ...overrides,
    }) as SiteTemplatePreviewDto;

  /** An ABP error response: the HTTP error's body is `{ error: { code, message } }`. */
  const apiError = (code: string, message: string) => ({ error: { error: { code, message } } });

  function create(
    overrides: { id?: string | null; templateService?: object; toaster?: object; router?: object } = {},
  ) {
    const id = overrides.id === undefined ? 't1' : overrides.id;
    const success = vi.fn();
    const navigate = vi.fn();

    TestBed.configureTestingModule({
      providers: [
        TemplateEditorComponent,
        {
          provide: SiteTemplateAdminService,
          useValue: {
            get: vi.fn(() => of(template())),
            getVersions: vi.fn(() => of({ items: [version(1), version(2)] })),
            getVersion: vi.fn((_id: string, number: number) => of(version(number))),
            preview: vi.fn(() => of(previewResult())),
            ...overrides.templateService,
          },
        },
        {
          provide: EnvironmentService,
          useValue: { getEnvironment: () => ({ apis: {} }), getApiUrl: () => API_URL },
        },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
        { provide: ToasterService, useValue: overrides.toaster ?? { success } },
        { provide: Router, useValue: overrides.router ?? { navigate } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } },
        },
      ],
    });

    return { component: TestBed.inject(TemplateEditorComponent), success, navigate };
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
    it('saves a draft as a new version with publish: false, then previews it', () => {
      const update = vi.fn(() => of(template({ latestVersionNumber: 3 })));
      const preview = vi.fn(() => of(previewResult({ versionNumber: 3 })));
      const { component, success } = create({ templateService: { update, preview } });

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
      expect(preview).toHaveBeenCalledWith('t1', 3, '/', { skipHandleError: true });
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
  });

  describe('renderPreview', () => {
    it('renders the selected version on the given path, with the API host as the base address', () => {
      const html = '<!doctype html><html><head><title>x</title></head><body>hi</body></html>';
      const preview = vi.fn(() => of(previewResult({ html })));
      const { component } = create({ templateService: { preview } });

      component.selectedVersion.set(2);
      component.previewPath.set('/about');
      component.renderPreview();

      expect(preview).toHaveBeenCalledWith('t1', 2, '/about', { skipHandleError: true });
      expect(component.previewSrcdoc()).toBe(
        '<!doctype html><html><head><base href="https://api.example/"><title>x</title></head><body>hi</body></html>',
      );
      expect(component.previewError()).toBeNull();
      expect(component.isPreviewing()).toBe(false);
    });

    it('puts the server message in the preview area when rendering fails', () => {
      const preview = vi.fn(() =>
        throwError(() => apiError('Site:090006', "No page of the site answers the path '/nope'.")),
      );
      const { component } = create({ templateService: { preview } });

      component.previewPath.set('/nope');
      component.renderPreview();

      expect(component.previewError()).toBe("No page of the site answers the path '/nope'.");
      expect(component.previewSrcdoc()).toBe('');
      expect(component.preview()).toBeUndefined();
    });

    it('notes the template the page normally uses, unless this is the layout', () => {
      const preview = vi.fn(() => of(previewResult({ pageTemplate: 'Blog/Article' })));
      const { component } = create({ templateService: { preview } });

      component.renderPreview();
      expect(component.pageTemplateNote()).toBe('Blog/Article');

      component.template.set(template({ name: 'layout' }));
      expect(component.pageTemplateNote()).toBeNull();

      component.template.set(template({ name: '/Blog/Article' }));
      expect(component.pageTemplateNote()).toBeNull();
    });
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

describe('withBaseHref', () => {
  it('inserts the base element right after the opening head tag, whatever its case or attributes', () => {
    expect(withBaseHref('<HEAD data-x="1"><title>t</title></HEAD>', 'https://api.example/')).toBe(
      '<HEAD data-x="1"><base href="https://api.example/"><title>t</title></HEAD>',
    );
  });

  it('puts it in front of a document that has no head, and does not mistake <header> for one', () => {
    expect(withBaseHref('<header>x</header>', 'https://api.example/')).toBe(
      '<base href="https://api.example/"><header>x</header>',
    );
  });

  it('leaves the document alone when there is no host to resolve against', () => {
    expect(withBaseHref('<head></head>', '')).toBe('<head></head>');
  });

  it('does not expand $ sequences in the document', () => {
    expect(withBaseHref('<head>$&</head>', 'https://a/')).toBe('<head><base href="https://a/">$&</head>');
  });
});
