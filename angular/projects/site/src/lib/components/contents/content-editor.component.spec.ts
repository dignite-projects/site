import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { DefaultQueueManager, EnvironmentService, QUEUE_MANAGER } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { CKEDITOR_UPLOAD_PROVIDER } from '@dignite/ng.flex-fields-ckeditor';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { Subject, firstValueFrom, from, of } from 'rxjs';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import { ContentTypeAdminService } from '../../proxy/dignite/site/admin/content-types/content-type-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import { SiteReferenceDataService } from '../../services/site-reference-data.service';
import { SiteCKEditorUploadProvider } from '../../files/services/site-ckeditor-upload-provider';
import { SiteFileUploadService } from '../../files/services/site-file-upload.service';
import { SiteUploadCleanup } from '../../files/services/site-upload-cleanup';
import { ContentEditorComponent } from './content-editor.component';

/**
 * The host layout puts the routed component under OnPush ancestors (`abp-router-outlet`, the LeptonX
 * layouts), which a plain-field write in an HTTP callback never marks for check - the editor would stay
 * on its blank loading state until an unrelated event did. See `content-types.component.spec.ts`.
 */
@Component({
  selector: 'lib-test-on-push-ancestor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ContentEditorComponent],
  template: '<site-content-editor />',
})
class OnPushAncestorComponent {}

@Component({
  selector: 'lib-test-root',
  imports: [OnPushAncestorComponent],
  template: '<lib-test-on-push-ancestor />',
})
class RootComponent {}

describe('ContentEditorComponent under an OnPush ancestor', () => {
  let schema$: Subject<{ enabledLanguages: string[]; defaultLanguage: string }>;
  const upload = vi.fn();
  const warn = vi.fn();

  beforeEach(() => {
    schema$ = new Subject();
    warn.mockReset();
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: SiteFileUploadService, useValue: { upload } },
        { provide: QUEUE_MANAGER, useClass: DefaultQueueManager },
        { provide: ContentAdminService, useValue: {} },
        { provide: ContentTypeAdminService, useValue: {} },
        { provide: PageAdminService, useValue: {} },
        {
          provide: SiteReferenceDataService,
          useValue: { getSchema: () => schema$, getFieldsById: () => of(new Map()) },
        },
        { provide: ToasterService, useValue: {} },
        { provide: ConfirmationService, useValue: { warn } },
      ],
    });
  });

  it('shows the form once the data arrives, with no event to mark the ancestors', () => {
    const fixture = TestBed.createComponent(RootComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();

    schema$.next({ enabledLanguages: ['en', 'ja'], defaultLanguage: 'en' });
    schema$.complete();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
    expect(
      [...fixture.nativeElement.querySelectorAll('select[formcontrolname="cultureName"] option')].map(
        (option: HTMLOptionElement) => option.value,
      ),
    ).toEqual(['en', 'ja']);
  });

  describe('the files an edit uploads', () => {
    function createEditor() {
      const fixture = TestBed.createComponent(RootComponent);
      fixture.detectChanges();
      const editor = fixture.debugElement.query(By.directive(ContentEditorComponent));
      return { fixture, editor, cleanup: editor.injector.get(SiteUploadCleanup) };
    }

    it("gives its CKEditor fields an upload provider that reports to the editor's own cleanup", async () => {
      const stored = { id: 'f1', name: 'a.webp', url: '/blobs/images/a.webp' };
      upload.mockReturnValue(of(stored));
      TestBed.inject(EnvironmentService).setState({
        apis: { default: { url: 'https://api.example' }, SiteAdmin: { url: 'https://api.example' } },
      } as never);
      const { editor, cleanup } = createEditor();
      const uploaded = vi.spyOn(cleanup, 'uploaded');

      const provider = editor.injector.get(CKEDITOR_UPLOAD_PROVIDER);
      await firstValueFrom(from(provider.upload(new File(['x'], 'photo.png'), 'images')));

      expect(provider).toBeInstanceOf(SiteCKEditorUploadProvider);
      expect(uploaded).toHaveBeenCalledWith(stored);
    });

    it('deletes them when the editor is left without a save', () => {
      const { fixture, cleanup } = createEditor();
      const discard = vi.spyOn(cleanup, 'discard').mockReturnValue(of(undefined));

      fixture.destroy();

      expect(discard).toHaveBeenCalledOnce();
    });

    it('leaves them to a save that is still running when the editor is left', () => {
      const { fixture, editor, cleanup } = createEditor();
      const discard = vi.spyOn(cleanup, 'discard').mockReturnValue(of(undefined));
      editor.componentInstance.isBusy.set(true);

      fixture.destroy();

      expect(discard).not.toHaveBeenCalled();
    });
  });

  describe('leaving the editor', () => {
    const stored = { id: 'f1', name: 'a.webp', url: '/blobs/images/a.webp' };

    function createLoadedEditor() {
      const fixture = TestBed.createComponent(RootComponent);
      fixture.detectChanges();
      schema$.next({ enabledLanguages: ['en'], defaultLanguage: 'en' });
      schema$.complete();
      fixture.detectChanges();
      const debugElement = fixture.debugElement.query(By.directive(ContentEditorComponent));
      const editor = debugElement.componentInstance as ContentEditorComponent;
      return { fixture, editor, cleanup: debugElement.injector.get(SiteUploadCleanup) };
    }

    it('lets an untouched editor go without asking', () => {
      const { editor } = createLoadedEditor();

      expect(editor.canLeave()).toBe(true);
      expect(warn).not.toHaveBeenCalled();
    });

    it('asks when the form was changed, and goes only when the user confirms', async () => {
      const { editor } = createLoadedEditor();
      editor.form!.markAsDirty();

      warn.mockReturnValueOnce(of(Confirmation.Status.confirm));
      await expect(firstValueFrom(editor.canLeave() as never)).resolves.toBe(true);
      expect(warn).toHaveBeenCalledWith('Site::LeaveEditorConfirm', 'AbpUi::AreYouSure');

      warn.mockReturnValueOnce(of(Confirmation.Status.reject));
      await expect(firstValueFrom(editor.canLeave() as never)).resolves.toBe(false);

      warn.mockReturnValueOnce(of(Confirmation.Status.dismiss));
      await expect(firstValueFrom(editor.canLeave() as never)).resolves.toBe(false);
    });

    it('says the uploaded files are deleted when the edit uploaded any', async () => {
      const { editor, cleanup } = createLoadedEditor();
      cleanup.uploaded(stored);
      vi.spyOn(cleanup, 'discard').mockReturnValue(of(undefined));
      warn.mockReturnValueOnce(of(Confirmation.Status.reject));

      await expect(firstValueFrom(editor.canLeave() as never)).resolves.toBe(false);

      expect(warn).toHaveBeenCalledWith('Site::LeaveEditorWithUploadsConfirm', 'AbpUi::AreYouSure');
    });

    it('does not ask while the content is being saved', () => {
      const { editor } = createLoadedEditor();
      editor.form!.markAsDirty();
      editor.isBusy.set(true);

      expect(editor.canLeave()).toBe(true);
      expect(warn).not.toHaveBeenCalled();
    });

    it('does not ask once the content is saved, though the form is still marked changed', () => {
      const { editor } = createLoadedEditor();
      const content = TestBed.inject(ContentAdminService) as unknown as { create: unknown };
      content.create = vi.fn().mockReturnValue(of({}));
      (TestBed.inject(ToasterService) as unknown as { success: unknown }).success = vi.fn();
      vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
      editor.form!.get('contentTypeId')!.setValue('type-1');
      editor.form!.markAsDirty();

      editor.save();

      expect(editor.canLeave()).toBe(true);
      expect(warn).not.toHaveBeenCalled();
    });

    it("has the browser ask when the tab is closed or reloaded with something unsaved", () => {
      const { editor } = createLoadedEditor();
      const untouched = new Event('beforeunload', { cancelable: true });
      window.dispatchEvent(untouched);
      expect(untouched.defaultPrevented).toBe(false);

      editor.form!.markAsDirty();
      const unsaved = new Event('beforeunload', { cancelable: true });
      window.dispatchEvent(unsaved);

      expect(unsaved.defaultPrevented).toBe(true);
    });
  });
});
