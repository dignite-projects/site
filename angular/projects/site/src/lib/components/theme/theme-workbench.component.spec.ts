import { LocalizationService, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
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
    layouts: [],
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

  describe('setting the default layout from its file', () => {
    const layouts = ['layouts/default', 'layouts/landing'];

    it('is offered for a file the check lists as a layout, unless it already is the default', () => {
      const { component } = create({ check: vi.fn(() => of(check({ layouts }))) });

      component.openFile('layouts/landing');
      expect(component.isLayoutInFront()).toBe(true);
      expect(component.isDefaultLayoutInFront()).toBe(false);

      component.openFile('layouts/default');
      expect(component.isLayoutInFront()).toBe(true);
      expect(component.isDefaultLayoutInFront()).toBe(true);

      component.openFile('Events');
      expect(component.isLayoutInFront()).toBe(false);
      expect(component.isDefaultLayoutInFront()).toBe(false);
    });

    it('sets the file as the default layout, says so, and checks again', () => {
      const update = vi.fn(() => of({ defaultLayout: 'layouts/landing', concurrencyStamp: 's2' }));
      const { component, service, toaster } = create({ check: vi.fn(() => of(check({ layouts }))), update });
      component.openFile('layouts/landing');
      service.check.mockClear();

      component.setAsDefaultLayout();

      expect(update).toHaveBeenCalledWith(
        { defaultLayout: 'layouts/landing', concurrencyStamp: 's' },
        { skipHandleError: true },
      );
      expect(component.theme()?.defaultLayout).toBe('layouts/landing');
      expect(component.isDefaultLayoutInFront()).toBe(true);
      expect(toaster.success).toHaveBeenCalledWith('Site::DefaultLayoutSet', undefined, { messageLocalizationParams: ['layouts/landing'] });
      expect(service.check).toHaveBeenCalled();
    });

    it('does nothing for the file that already is the default', () => {
      const update = vi.fn();
      const { component } = create({ check: vi.fn(() => of(check({ layouts }))), update });
      component.openFile('layouts/default');

      component.setAsDefaultLayout();

      expect(update).not.toHaveBeenCalled();
    });

    it('shows why the server refuses, and leaves the default layout as it was', () => {
      const update = vi.fn(() => throwError(() => ({ error: { error: { message: "'layouts/landing' is not a layout" } } })));
      const { component, toaster } = create({ check: vi.fn(() => of(check({ layouts }))), update });
      component.openFile('layouts/landing');

      component.setAsDefaultLayout();

      expect(toaster.error).toHaveBeenCalledWith("'layouts/landing' is not a layout");
      expect(component.theme()?.defaultLayout).toBe('layouts/default');
      expect(component.isBusy()).toBe(false);
    });
  });

  describe('the check', () => {
    /** A service whose checks answer when the test says so, and in the order it says. */
    function createWithPendingChecks() {
      const answers: Subject<SiteThemeCheckDto>[] = [];
      const check = vi.fn(() => {
        const answer = new Subject<SiteThemeCheckDto>();
        answers.push(answer);
        return answer;
      });
      const created = create({ check });
      // The one asked for on load: answered at once.
      answers[0].next(check_({ fingerprint: 'load' }));
      return { ...created, answers };
    }

    const check_ = (overrides: Partial<SiteThemeCheckDto> = {}) => ({
      fingerprint: 'fp',
      issues: [],
      hasErrors: false,
      layouts: [],
      changes: [],
      hasChanges: false,
      ...overrides,
    });

    it('keeps the answer of the last check when an earlier one answers late', () => {
      const { component, answers } = createWithPendingChecks();

      component.runCheck();
      component.runCheck();
      expect(component.isChecking()).toBe(true);

      answers[2].next(check_({ fingerprint: 'newer' }));
      expect(component.check()?.fingerprint).toBe('newer');
      expect(component.isChecking()).toBe(false);

      answers[1].next(check_({ fingerprint: 'older' }));
      expect(component.check()?.fingerprint).toBe('newer');
    });

    it('stays checking until the last check is answered', () => {
      const { component, answers } = createWithPendingChecks();

      component.runCheck();
      component.runCheck();
      answers[1].next(check_({ fingerprint: 'older' }));

      expect(component.isChecking()).toBe(true);
      expect(component.check()?.fingerprint).toBe('load');
    });

    it('hands the answer to a callback whose check a later one took over from', () => {
      const { component, answers } = createWithPendingChecks();
      const after = vi.fn();

      component.runCheck(after);
      component.runCheck();
      answers[1].next(check_({ fingerprint: 'older' }));
      expect(after).not.toHaveBeenCalled();

      answers[2].next(check_({ fingerprint: 'newer' }));
      expect(after).toHaveBeenCalledTimes(1);
      expect(after).toHaveBeenCalledWith(expect.objectContaining({ fingerprint: 'newer' }));
    });

    it('stops checking when the last check fails, and drops what waited for it', () => {
      const { component, answers } = createWithPendingChecks();
      const after = vi.fn();

      component.runCheck(after);
      answers[1].error({ status: 500 });

      expect(component.isChecking()).toBe(false);
      expect(component.check()?.fingerprint).toBe('load');
      expect(after).not.toHaveBeenCalled();
    });
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

    it('previews the theme at the home page whichever file is in front', () => {
      const tab = { opener: {}, location: { replace: vi.fn() }, close: vi.fn() };
      vi.spyOn(window, 'open').mockReturnValue(tab as unknown as Window);
      const getPreviewLink = vi.fn(() => of({ url: 'https://site.example/?site-preview=t' }));
      const { component } = create({ getPreviewLink }, {}, [
        { id: 'p-events', name: 'events', displayName: 'Events', route: '/events/{slug?}', template: 'Events', isActive: true },
      ]);
      component.openFile('Events');

      component.openPreview();

      expect(getPreviewLink).toHaveBeenCalledWith(undefined, undefined);
    });

    it('finds the active pages the template in front renders', () => {
      const { component } = create({}, {}, [
        { id: 'p-events', name: 'events', displayName: 'Events', route: '/events/{slug?}', template: 'Events', isActive: true },
        { id: 'p-news', name: 'news', displayName: '', route: '/news/{year}/{slug}', template: 'Events.cshtml', isActive: true },
        { name: 'old', displayName: 'Old', route: '/old', template: 'Events', isActive: false },
        { name: 'about', displayName: 'About', route: '/about', template: 'Default', isActive: true },
      ]);

      expect(component.templatePages()).toEqual([]);
      component.openFile('Events');

      expect(component.templatePages()).toEqual([
        { id: 'p-events', name: 'Events' },
        { id: 'p-news', name: 'news' },
      ]);
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

  describe('tabs', () => {
    it('gives the tab of a file only looked at to the next one', () => {
      const { component } = create();

      component.openFile('Events');
      component.openFile('About');
      component.openFile('Home');

      expect(component.openFiles().map(f => f.path)).toEqual(['Home']);
      expect(component.activePath()).toBe('Home');
    });

    it('keeps the tab of a file once it is edited, until it is closed', () => {
      const { component } = create();
      component.openFile('Events');
      component.onEdit('<p>changed</p>');

      component.openFile('About');
      component.openFile('Home');

      expect(component.openFiles().map(f => f.path)).toEqual(['Events', 'Home']);
    });

    it('keeps the tab of an edited file after it is saved', () => {
      const saveFile = vi.fn(() => of(file('Events', '<p>changed</p>', 'stamp-2')));
      const { component } = create({ saveFile });
      component.openFile('Events');
      component.onEdit('<p>changed</p>');
      component.save();

      component.openFile('About');

      expect(component.isDirty(component.openFiles()[0])).toBe(false);
      expect(component.openFiles().map(f => f.path)).toEqual(['Events', 'About']);
    });

    it('does not keep a file for an edit that changes nothing', () => {
      const { component } = create();
      component.openFile('Events');

      component.onEdit('<p>x</p>');
      component.openFile('About');

      expect(component.openFiles().map(f => f.path)).toEqual(['About']);
    });

    it('keeps a file that was just created', () => {
      const saveFile = vi.fn(() => of(file('Fresh')));
      const { component } = create({ saveFile });
      component.openFile('Events');

      component.openNewFile();
      component.newFilePath.set('Fresh');
      component.createFile();
      component.openFile('About');

      expect(component.openFiles().map(f => f.path)).toEqual(['Fresh', 'About']);
    });
  });

  describe('the more menu of a file', () => {
    function click(bottom: number, right: number): MouseEvent {
      const button = document.createElement('button');
      vi.spyOn(button, 'getBoundingClientRect').mockReturnValue({ bottom, right } as DOMRect);
      return { stopPropagation: vi.fn(), currentTarget: button } as unknown as MouseEvent;
    }

    it('opens under its button, closes on a second click, another click anywhere and Escape', () => {
      const { component } = create();
      const event = click(50, 200);

      component.toggleRowMenu(event, 'Events');
      expect(component.rowMenu()).toEqual({ path: 'Events', top: 50, right: window.innerWidth - 200 });
      expect(event.stopPropagation).toHaveBeenCalled();

      component.toggleRowMenu(event, 'Events');
      expect(component.rowMenu()).toBeNull();

      component.toggleRowMenu(event, 'Events');
      component.toggleRowMenu(click(80, 200), 'About');
      expect(component.rowMenu()?.path).toBe('About');

      component.closeRowMenu();
      expect(component.rowMenu()).toBeNull();
    });

    it('renames and deletes the file it was opened for, not the one in front', () => {
      const { component } = create();
      component.openFile('Events');
      const openRename = vi.spyOn(component, 'openRename').mockImplementation(() => undefined);
      const deleteFile = vi.spyOn(component, 'deleteFile').mockImplementation(() => undefined);

      component.toggleRowMenu(click(50, 200), 'About');
      component.renameFromMenu();
      component.toggleRowMenu(click(50, 200), 'Contact');
      component.deleteFromMenu();

      expect(openRename).toHaveBeenCalledWith('About');
      expect(deleteFile).toHaveBeenCalledWith('Contact');
      expect(component.rowMenu()).toBeNull();
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

    it('shows no fields for a file no page renders, and reads nothing', () => {
      const { component, pageService, fieldService } = create({}, {}, pages, library);

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
