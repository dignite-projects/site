import { LocalizationService, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
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

  function create(themeService: object = {}, confirmation: object = {}) {
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
        { provide: ConfirmationService, useValue: confirmation },
        { provide: ToasterService, useValue: toaster },
        { provide: LocalizationService, useValue: { instant: (key: string) => key } },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => true } },
      ],
    });

    const component = TestBed.inject(ThemeWorkbenchComponent);
    component.ngOnInit();
    return { component, service, toaster };
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
