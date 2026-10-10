import { inject } from '@angular/core';
import type { CKEditorConfigContributor } from '@dignite/ng.flex-fields-ckeditor';
// Type-only: a value import from 'ckeditor5' would pull the whole package out of the lazy chunk
// @dignite/ng.flex-fields-ckeditor loads it in.
import type { DowncastAttributeEvent, Editor, Emitter, ModelElement, UpcastElementEvent } from 'ckeditor5';
import { SITE_FILE_PATH } from './site-file-url';
import { SiteFileUrlService } from './site-file-url.service';

const IMAGE_TYPES = ['imageBlock', 'imageInline'] as const;

/**
 * Lets a CKEditor field work with Site's images on the `SiteAdmin` API's host while storing them relative.
 * A stored address is relative (`<img src="/api/site-public/files/...">`, see `SITE_FILE_PATH`), and this
 * admin UI is served from another origin, where it does not resolve.
 *
 * So the editor's **model** holds the absolute address, and only the data the editor hands out is
 * relative: a plugin converts an image's `src` (`imageBlock`/`imageInline`) on the way in (upcast:
 * `setData()`, the initial value, a paste) from relative to absolute, and on the way out (data downcast:
 * `getData()` - the form value that is saved, the Source view, a copy) from absolute back to relative.
 * Everything CKEditor itself does with the model's `src` then just works: the editing view shows it, and
 * `ImageUtils.setImageNaturalSizeAttributes` reads an uploaded image's size from it (`width`/`height`).
 * HTML and Markdown fields alike: both load images into the same model. Links (`<a href>`) are left alone.
 * The upload provider hands the editor the absolute address for the same reason.
 *
 * Registered by `provideSite()` under `CKEDITOR_CONFIG_CONTRIBUTORS` (with `useFactory`, so it can inject);
 * the pattern is the one in `@dignite/ng.flex-fields-ckeditor`'s README, "Customizing the editor
 * configuration".
 */
export function siteCKEditorConfigContributor(): CKEditorConfigContributor {
  const fileUrls = inject(SiteFileUrlService);

  return config => {
    const apiBase = fileUrls.apiBase;
    if (!apiBase) {
      // The UI shares its API's origin: a relative address already loads.
      return;
    }

    const absolutePrefix = apiBase + SITE_FILE_PATH;
    const toModel = (src: string) => (src.startsWith(SITE_FILE_PATH) ? apiBase + src : src);
    const toData = (src: string) => (src.startsWith(absolutePrefix) ? src.substring(apiBase.length) : src);

    // A CKEditor 5 plugin can be a plain function of the editor.
    function SiteFileAddresses(editor: Editor): void {
      // In: the image plugin creates the model image, src included, in its own `element:img` converter
      // (`upcastImg`, normal priority; a <figure> converts its <img> through the same event). This listener
      // runs after it ('low') and corrects the src of the image it produced.
      editor.conversion.for('upcast').add(upcastDispatcher => {
        (upcastDispatcher as unknown as Emitter).on<UpcastElementEvent>(
          'element:img',
          (evt, data, conversionApi) => {
            if (!data.modelRange) {
              return;
            }

            for (const item of data.modelRange.getItems({ shallow: true })) {
              if (!IMAGE_TYPES.some(type => item.is('element', type))) {
                continue;
              }

              const src = item.getAttribute('src');
              if (typeof src === 'string' && src !== toModel(src)) {
                conversionApi.writer.setAttribute('src', toModel(src), item as ModelElement);
              }
            }
          },
          { priority: 'low' },
        );
      });

      // Out: the image plugin's own src converter (`downcastImageAttribute`, registered for both downcast
      // pipelines) writes the model's src as it is. In the data pipeline only, this one runs first ('high')
      // and consumes the change, so that one skips it; the editing view keeps the absolute address.
      editor.conversion.for('dataDowncast').add(downcastDispatcher => {
        for (const imageType of IMAGE_TYPES) {
          (downcastDispatcher as unknown as Emitter).on<DowncastAttributeEvent<ModelElement>>(
            `attribute:src:${imageType}`,
            (evt, data, conversionApi) => {
              if (!conversionApi.consumable.consume(data.item, evt.name)) {
                return;
              }

              const view = conversionApi.mapper.toViewElement(data.item);
              const img = view && editor.plugins.get('ImageUtils').findViewImgElement(view);
              if (img) {
                const src = (data.attributeNewValue as string | null) ?? '';
                conversionApi.writer.setAttribute('src', toData(src), img);
              }
            },
            { priority: 'high' },
          );
        }
      });
    }
    // The casts to Emitter above: both dispatchers are Emitters (EmitterMixin), but this workspace compiles
    // without strictNullChecks, under which ckeditor5's EmitterMixinConstructor<undefined> resolves to the
    // wrong branch and their types lose `on`.

    config.extraPlugins = [...(config.extraPlugins ?? []), SiteFileAddresses];
  };
}
