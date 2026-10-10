import { TestBed } from '@angular/core/testing';
import { EnvironmentService } from '@abp/ng.core';
import type { CKEditorConfigContext } from '@dignite/ng.flex-fields-ckeditor';
import type { ClassicEditor, EditorConfig } from 'ckeditor5';
import { siteCKEditorConfigContributor } from './site-ckeditor-config-contributor';

describe('siteCKEditorConfigContributor', () => {
  const API = 'https://api.example';
  const RELATIVE = '/api/site-public/files/site-images/abc.png?__tenant=';

  function contribute(apis: object | undefined, config: EditorConfig): EditorConfig | void {
    if (apis) {
      TestBed.inject(EnvironmentService).setState({ apis } as never);
    }

    const contributor = TestBed.runInInjectionContext(() => siteCKEditorConfigContributor());
    return contributor(config, {} as CKEditorConfigContext);
  }

  const apis = { default: { url: 'https://default.example' }, SiteAdmin: { url: `${API}/` } };

  it('adds a plugin, after the ones already there, when the API is on another origin', () => {
    const existing = () => undefined;
    const config: EditorConfig = { extraPlugins: [existing] };

    contribute(apis, config);

    expect(config.extraPlugins).toHaveLength(2);
    expect(config.extraPlugins![0]).toBe(existing);
  });

  it('leaves the configuration alone when the UI shares its API origin', () => {
    const config: EditorConfig = {};

    contribute(undefined, config);

    expect(config.extraPlugins).toBeUndefined();
  });

  describe('in a real editor', () => {
    let editor: ClassicEditor;

    beforeEach(async () => {
      // jsdom has no ResizeObserver; the editor's toolbar uses one to group its items.
      if (!('ResizeObserver' in window)) {
        vi.stubGlobal(
          'ResizeObserver',
          class {
            observe(): void {}
            unobserve(): void {}
            disconnect(): void {}
          },
        );
      }

      const ckeditor5 = await import('ckeditor5');
      const config: EditorConfig = {
        licenseKey: 'GPL',
        plugins: [ckeditor5.Essentials, ckeditor5.Paragraph, ckeditor5.Link, ckeditor5.Image, ckeditor5.ImageInline],
      };
      contribute(apis, config);

      const element = document.createElement('div');
      document.body.appendChild(element);
      editor = await ckeditor5.ClassicEditor.create(element, config);
    });

    afterEach(async () => {
      await editor?.destroy();
      vi.unstubAllGlobals();
    });

    function modelSources(): unknown[] {
      const range = editor.model.createRangeIn(editor.model.document.getRoot()!);
      return [...range.getItems()]
        .filter(item => item.is('element', 'imageBlock') || item.is('element', 'imageInline'))
        .map(item => (item as { getAttribute(key: string): unknown }).getAttribute('src'));
    }

    it('holds a relative Site address absolute in the model and hands it out relative', () => {
      editor.setData(`<figure class="image"><img src="${RELATIVE}"></figure><p>a <img src="${RELATIVE}"> b</p>`);

      expect(modelSources()).toEqual([API + RELATIVE, API + RELATIVE]);
      const data = editor.getData();
      expect(data).toContain(`<img src="${RELATIVE}">`);
      expect(data).not.toContain(API);
      // The editing view - what the user sees - loads it from the API host.
      expect(editor.editing.view.getDomRoot()!.querySelector('img')!.getAttribute('src')).toBe(API + RELATIVE);
    });

    it('saves an absolute address on the API host - an upload - relative', () => {
      editor.setData('<p>x</p>');
      editor.model.change(writer => {
        const image = writer.createElement('imageBlock', { src: API + RELATIVE });
        editor.model.insertContent(image, editor.model.document.getRoot()!, 'end');
      });

      expect(editor.getData()).toContain(`<img src="${RELATIVE}">`);
    });

    it('leaves other addresses and links alone', () => {
      const external = 'https://cdn.example/a.png';
      editor.setData(`<figure class="image"><img src="${external}"></figure><p><a href="${RELATIVE}">file</a></p>`);

      expect(modelSources()).toEqual([external]);
      expect(editor.getData()).toContain(`<img src="${external}">`);
      expect(editor.getData()).toContain(`<a href="${RELATIVE}">file</a>`);
    });
  });
});
