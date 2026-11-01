import { LocalizationService, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { FieldAdminService } from '../../proxy/dignite/site/admin/fields/field-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import { SiteThemeAdminService } from '../../proxy/dignite/site/admin/themes/site-theme-admin.service';
import type { SiteThemeCheckDto, SiteThemeFileDto } from '../../proxy/dignite/site/admin/themes/models';
import { SiteThemeFileStatus } from '../../proxy/dignite/site/themes/site-theme-file-status.enum';
import { ThemeWorkbenchComponent } from './theme-workbench.component';

describe('ThemeWorkbenchComponent', () => {
  const file = (path: string, body = '<p>x</p>', stamp = 'stamp-1'): SiteThemeFileDto => ({
    path,
    isAsset: path.includes('.'),
    body,
    concurrencyStamp: stamp,
    syntaxErrors: [],
  });

  const check = (overrides: Partial<SiteThemeCheckDto> = {}): SiteThemeCheckDto => ({
    fingerprint: 'fp',
    issues: [],
    hasErrors: false,
    changes: [{ path: 'Events', status: SiteThemeFileStatus.Modified }],
    hasChanges: true,
    ...overrides,
  });

  function create(
    themeService: object = {},
    confirmation: object = {},
    pages: object[] = [],
    fieldLibrary: object[] = [],
  ) {
    const pageService = {
      getList: vi.fn(() => of({ items: pages })),
      get: vi.fn((id: string) => of(pages.find(page => (page as { id?: string }).id === id))),
    };
    const fieldService = { getList: vi.fn(() => of({ items: fieldLibrary })) };
    const toaster = { success: vi.fn(), error: vi.fn(), info: vi.fn(), warn: vi.fn() };
    const service = {
      get: vi.fn(() => of({ defaultLayout: 'layouts/default', concurrencyStamp: 's' })),
      getFiles: vi.fn(() =>
        of({ items: [{ path: 'Events', isAsset: false, status: SiteThemeFileStatus.Unchanged }] })),
      getFile: vi.fn((path: string) => of(file(path))),
      check: vi.fn(() => of(check())),
      ...themeService,
    };

    TestBed.configureTestingModule({
      providers: [
        ThemeWorkbenchComponent,
        { provide: SiteThemeAdminService, useValue: service },
        { provide: PageAdminService, useValue: pageService },
        { provide: FieldAdminService, useValue: fieldService },
        { provide: ConfirmationService, useValue: confirmation },
        { provide: ToasterService, useValue: toaster },
        { provide: LocalizationService, useValue: { instant: (key: string) => key } },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
      ],
    });

    const component = TestBed.inject(ThemeWorkbenchComponent);
    component.ngOnInit();
    return { component, service, toaster, pageService, fieldService };
  }

  it('loads the theme, its files and the check', () => {
    const { component, service } = create();

    expect(component.isLoading()).toBe(false);
    expect(component.theme()?.defaultLayout).toBe('layouts/default');
    expect(component.treeRows().map(row => row.node.path)).toEqual(['Events']);
    expect(service.check).toHaveBeenCalled();
    expect(component.check()?.fingerprint).toBe('fp');
  });

  it('opens a file in a tab once, and tells unsaved changes', () => {
    const { component, service } = create();

    component.openFile('Events');
    component.openFile('Events');

    expect(service.getFile).toHaveBeenCalledTimes(1);
    expect(component.openFiles().map(f => f.path)).toEqual(['Events']);
    expect(component.activePath()).toBe('Events');

    component.onEdit('<p>changed</p>');

    expect(component.isDirty(component.activeFile()!)).toBe(true);
    expect(component.hasUnsavedFiles()).toBe(true);
  });

  it('saves with the stamp it read, keeps the saved body and checks again', () => {
    const saveFile = vi.fn(() => of(file('Events', '<p>changed</p>', 'stamp-2')));
    const { component, service, toaster } = create({ saveFile });
    component.openFile('Events');
    component.onEdit('<p>changed</p>');
    service.check.mockClear();

    component.save();

    expect(saveFile).toHaveBeenCalledWith(
      { path: 'Events', body: '<p>changed</p>', concurrencyStamp: 'stamp-1' },
      { skipHandleError: true },
    );
    const saved = component.activeFile()!;
    expect(saved.concurrencyStamp).toBe('stamp-2');
    expect(component.isDirty(saved)).toBe(false);
    expect(toaster.success).toHaveBeenCalledWith('Site::ThemeFileSaved');
    expect(service.check).toHaveBeenCalledTimes(1);
  });

  it('asks on a conflict, and overwrites without the stamp when told to', () => {
    const saveFile = vi
      .fn()
      .mockReturnValueOnce(throwError(() => ({ status: 409 })))
      .mockReturnValueOnce(of(file('Events', '<p>mine</p>', 'stamp-3')));
    const warn = vi.fn(() => of(Confirmation.Status.confirm));
    const { component } = create({ saveFile }, { warn });
    component.openFile('Events');
    component.onEdit('<p>mine</p>');

    component.save();

    expect(warn).toHaveBeenCalledWith('Site::SaveConflictConfirm', 'AbpUi::AreYouSure', expect.objectContaining({
      messageLocalizationParams: ['Events'],
    }));
    expect(saveFile).toHaveBeenLastCalledWith(
      { path: 'Events', body: '<p>mine</p>', concurrencyStamp: undefined },
      { skipHandleError: true },
    );
  });

  it('loads their version on a conflict when told to, and keeps the edits when the question is closed', () => {
    const saveFile = vi.fn(() => throwError(() => ({ status: 409 })));
    const getFile = vi
      .fn()
      .mockReturnValueOnce(of(file('Events')))
      .mockReturnValueOnce(of(file('Events', '<p>theirs</p>', 'stamp-9')));
    const warn = vi
      .fn()
      .mockReturnValueOnce(of(Confirmation.Status.dismiss))
      .mockReturnValueOnce(of(Confirmation.Status.reject));
    const { component } = create({ saveFile, getFile }, { warn });
    component.openFile('Events');
    component.onEdit('<p>mine</p>');

    component.save();
    expect(component.activeFile()!.body).toBe('<p>mine</p>');
    expect(getFile).toHaveBeenCalledTimes(1);

    component.save();
    expect(component.activeFile()!.body).toBe('<p>theirs</p>');
    expect(component.activeFile()!.concurrencyStamp).toBe('stamp-9');
  });

  it('asks before closing a file with unsaved changes', () => {
    const warn = vi.fn(() => of(Confirmation.Status.dismiss));
    const { component } = create({}, { warn });
    component.openFile('Events');
    component.onEdit('<p>changed</p>');

    component.closeFile('Events');

    expect(warn).toHaveBeenCalled();
    expect(component.openFiles().length).toBe(1);
  });

  it('refuses a path the server would refuse, without asking it', () => {
    const saveFile = vi.fn();
    const { component } = create({ saveFile });

    component.openNewFile();
    component.newFilePath.set('post.html');
    component.createFile();

    expect(saveFile).not.toHaveBeenCalled();
    expect(component.newFileError()).toBe('Site::ThemePathExtensionNotAllowed');
  });

  it('creates a file from its skeleton and opens it', () => {
    const saveFile = vi.fn((input: { path: string; body: string }) => of(file(input.path, input.body)));
    const { component } = create({ saveFile });

    component.openNewFile('layouts');
    expect(component.newFilePath()).toBe('layouts/');
    component.newFilePath.set('layouts/landing');
    component.createFile();

    const [input] = saveFile.mock.calls[0];
    expect(input.path).toBe('layouts/landing');
    expect(input.body).toContain('{{ site_head }}');
    expect(component.activePath()).toBe('layouts/landing');
    expect(component.isNewFileOpen()).toBe(false);
  });

  it('publishes with the fingerprint of the check it showed', () => {
    const publish = vi.fn(() => of({ number: 3, changedFileCount: 1, isLive: true }));
    const { component, toaster } = create({ publish });

    component.openPublish();
    component.publishComment.set('New hero');
    component.publish();

    expect(publish).toHaveBeenCalledWith({ fingerprint: 'fp', comment: 'New hero' }, { skipHandleError: true });
    expect(toaster.success).toHaveBeenCalledWith('Site::ThemePublished', undefined, { messageLocalizationParams: ['3'] });
  });

  it('publishes on Enter in the comment, but not while an input method composes', () => {
    const publish = vi.fn(() => of({ number: 3, changedFileCount: 1, isLive: true }));
    const { component } = create({ publish });
    component.openPublish();

    component.publishOnEnter(new KeyboardEvent('keydown', { key: 'Enter', isComposing: true }));
    expect(publish).not.toHaveBeenCalled();

    component.publishOnEnter(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(publish).toHaveBeenCalledTimes(1);
  });

  it('does not publish a theme with errors', () => {
    const publish = vi.fn();
    const { component } = create({ publish, check: vi.fn(() => of(check({ hasErrors: true }))) });

    component.openPublish();
    component.publish();

    expect(publish).not.toHaveBeenCalled();
  });

  it('tells a default layout that the live release does not use', () => {
    const { component } = create();
    const live = { liveReleaseNumber: 3, liveDefaultLayout: 'layouts/default' };

    expect(component.defaultLayoutChanged(check({ ...live, defaultLayout: 'layouts/landing' }))).toBe(true);
    expect(component.defaultLayoutChanged(check({ ...live, defaultLayout: 'layouts/default' }))).toBe(false);
    expect(component.defaultLayoutChanged(check({ liveReleaseNumber: 3, defaultLayout: undefined, liveDefaultLayout: null as unknown as string }))).toBe(false);
    expect(component.defaultLayoutChanged(check({ defaultLayout: 'layouts/landing' }))).toBe(false);
  });

  describe('preview', () => {
    afterEach(() => vi.restoreAllMocks());

    it('opens a tab in the click and points it at the signed link, without a handle on this page', () => {
      const tab = { opener: {}, location: { replace: vi.fn() }, close: vi.fn() };
      vi.spyOn(window, 'open').mockReturnValue(tab as unknown as Window);
      const getPreviewLink = vi.fn(() => of({ url: 'https://site.example/?site-preview=t' }));
      const { component } = create({ getPreviewLink });

      component.openPreview('release-1');

      expect(window.open).toHaveBeenCalledWith('', '_blank');
      expect(tab.opener).toBeNull();
      expect(getPreviewLink).toHaveBeenCalledWith(undefined, 'release-1');
      expect(tab.location.replace).toHaveBeenCalledWith('https://site.example/?site-preview=t');
    });

    it('offers the active pages the template in front renders, with an address where a page has one', () => {
      const tab = { opener: {}, location: { replace: vi.fn() }, close: vi.fn() };
      vi.spyOn(window, 'open').mockReturnValue(tab as unknown as Window);
      const getPreviewLink = vi.fn(() => of({ url: 'https://site.example/events?site-preview=t' }));
      const { component } = create({ getPreviewLink }, {}, [
        { id: 'p-events', name: 'events', displayName: 'Events', route: '/events/{slug?}', template: 'Events', isActive: true },
        { id: 'p-news', name: 'news', displayName: '', route: '/news/{year}/{slug}', template: 'Events.cshtml', isActive: true },
        { name: 'old', displayName: 'Old', route: '/old', template: 'Events', isActive: false },
        { name: 'about', displayName: 'About', route: '/about', template: 'Default', isActive: true },
      ]);

      expect(component.previewPages()).toEqual([]);
      component.openFile('Events');

      expect(component.previewPages()).toEqual([
        { id: 'p-events', name: 'Events', route: '/events/{slug?}', path: '/events' },
        { id: 'p-news', name: 'news', route: '/news/{year}/{slug}', path: null },
      ]);

      component.isPreviewMenuOpen.set(true);
      component.openPreview(undefined, '/events');

      expect(getPreviewLink).toHaveBeenCalledWith('/events', undefined);
      expect(component.isPreviewMenuOpen()).toBe(false);
    });

    it('says so when the browser blocks the tab, and asks for no link', () => {
      vi.spyOn(window, 'open').mockReturnValue(null);
      const getPreviewLink = vi.fn();
      const { component, toaster } = create({ getPreviewLink });

      component.openPreview();

      expect(toaster.warn).toHaveBeenCalledWith('Site::PreviewPopupBlocked');
      expect(getPreviewLink).not.toHaveBeenCalled();
    });

    it('tells that unsaved changes are not previewed, and closes the tab when no link comes', () => {
      const tab = { opener: {}, location: { replace: vi.fn() }, close: vi.fn() };
      vi.spyOn(window, 'open').mockReturnValue(tab as unknown as Window);
      const getPreviewLink = vi.fn(() => throwError(() => ({ status: 403 })));
      const { component, toaster } = create({ getPreviewLink });
      component.openFile('Events');
      component.onEdit('<p>changed</p>');

      component.openPreview();

      expect(toaster.info).toHaveBeenCalledWith('Site::PreviewUnsavedNotShown');
      expect(tab.close).toHaveBeenCalled();
    });
  });

  describe('field sidebar', () => {
    const pages = [
      {
        id: 'p-events',
        name: 'events',
        displayName: 'Events',
        route: '/events/{slug?}',
        template: 'Events',
        isActive: true,
        contentTypes: [{ id: 'ct-event', displayName: 'Event', fields: [{ fieldId: 'f-title', order: 1 }] }],
      },
      { id: 'p-archive', name: 'archive', displayName: 'Archive', route: '/archive', template: 'Events', isActive: true, contentTypes: [] },
    ];
    const library = [{ id: 'f-title', name: 'title', displayName: 'Title', fieldTypeName: 'Text' }];

    it('lists the fields of a page the template in front renders, reading each page and the library once', () => {
      const { component, pageService, fieldService } = create({}, {}, pages, library);
      component.openFile('Events');

      component.rightPanel.set('fields');
      TestBed.tick();

      expect(component.showsFields()).toBe(true);
      expect(component.fieldsPageId()).toBe('p-events');
      expect(component.fieldGroups()).toEqual([
        {
          id: 'ct-event',
          displayName: 'Event',
          fields: [{ name: 'title', displayName: 'Title', fieldTypeName: 'Text', snippet: '{{ content.fields.title }}' }],
        },
      ]);

      component.chosenFieldsPageId.set('p-archive');
      TestBed.tick();
      expect(component.isLoadingFields()).toBe(false);
      expect(component.fieldGroups()).toEqual([]);

      component.chosenFieldsPageId.set('p-events');
      TestBed.tick();
      expect(pageService.get).toHaveBeenCalledTimes(2);
      expect(fieldService.getList).toHaveBeenCalledTimes(1);
    });

    it('shows the check for a file no page renders, and reads nothing', () => {
      const { component, pageService, fieldService } = create({}, {}, pages, library);
      component.rightPanel.set('fields');

      component.openFile('partials/card');
      TestBed.tick();

      expect(component.showsFields()).toBe(false);
      expect(pageService.get).not.toHaveBeenCalled();
      expect(fieldService.getList).not.toHaveBeenCalled();
    });
  });

  it('keeps a renamed file open under its new path', () => {
    const renameFile = vi.fn(() =>
      of({ file: file('shared/_card'), updatedPaths: ['Events'], defaultLayoutUpdated: false, pagesStillUsingOldPath: [] }));
    const { component } = create({ renameFile });
    component.openFile('_card');

    component.openRename('_card');
    component.renameTo.set('shared/_card');
    component.rename();

    expect(renameFile).toHaveBeenCalledWith(
      { path: '_card', newPath: 'shared/_card', updateReferences: true },
      { skipHandleError: true },
    );
    expect(component.openFiles().map(f => f.path)).toEqual(['shared/_card']);
    expect(component.activePath()).toBe('shared/_card');
  });
});
