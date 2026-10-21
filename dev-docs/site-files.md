# Site files: internals

How the file library is built. What it does - containers, upload rules, permissions, addresses, the public
read endpoint, CKEditor upload and the MCP options - is documented for users in
[docs/en/files.md](../docs/en/files.md); this page only covers what a contributor needs on top of that.

The library used to be a separate module in abp-modules (File Explorer); since 0.1.0-preview.25 it is part of
Site, under Site's namespaces, tables, permissions and routes. From abp-modules it uses only the upload
rules: the BLOB pipeline contributors of `Dignite.Abp.BlobStoring.Pipeline` and `Dignite.Abp.BlobStoring.Imaging`
(until abp-modules `10.0.0-rc.27` the `Dignite.Abp.FileStoring` packages and their `IFileStorer`).

## Where things live

| Piece | Location |
|---|---|
| Descriptors and directories (`FileDescriptor`, `DirectoryDescriptor`, their managers and repositories) | `Dignite.Site.Domain` - `Files/` |
| Tables | `SiteFileDescriptors`, `SiteDirectoryDescriptors` (EF Core); collections of the same names (MongoDB) |
| Containers | `SiteFileContainerNames`; the allowed types are one table, `SiteFileTypes` (extension and MIME type). Their upload rules are BLOB pipeline contributors set in `SiteAdminApplicationModule.ConfigureFileContainers`; a host only picks the storage provider |
| Admin API | `api/site-admin/files`, `api/site-admin/files/directories` (`IFileAdminAppService`, `IDirectoryAdminAppService`) |
| Public read | `GET`/`HEAD` `api/site-public/files/{containerName}/{blobName}` (`IFilePublicAppService`) |
| MCP tools | `site_list_file_containers`, `site_list_files`, `site_get_file`, `site_upload_file`, `site_update_file`, `site_delete_file`, `site_list_directories`, `site_create_directory` |
| File field type | `FileFieldType` in `Dignite.FlexFields.Site` (registration key `FileExplorer`), its view in `Dignite.Site.Public.Web/Views/Shared/FlexFields/` |
| Admin UI | `@dignite/ng.site`: the file picker, the browsing modal and the file field type (`src/lib/files`, `src/lib/field-types/file`) |

`SiteFileTypes` is the single source of both lists the library reports: the containers' upload policy allows
its **MIME types**, because the pipeline checks the type detected from the bytes, and the admin API reports
its **extensions** (`FileContainerConfigurationDto.AllowedFileTypeNames`), because that is what a file input's
`accept` and the MCP tools speak. Do not write a second list.

## The upload pipeline

Bytes go into the container through ABP's `IBlobContainer`, whose BLOB pipeline runs the container's
contributors inside `SaveAsync`, in this order: `MaxSize`, `AllowedContentTypes`, `ImageResize`,
`ImageCompress`. The image contributors leave anything that is not an image untouched. The image provider
is ABP's SkiaSharp one (`Volo.Abp.Imaging.SkiaSharp`), depended on by `Dignite.Site.Admin.Application` and
`Dignite.Site.Public.Application`, so a host does not pick one.

`FileDescriptorManager` is the caller around that pipeline. For an upload it:

1. copies it once into memory, capped at the container's size limit (`Dignite.Abp.BlobStoring:ContentTooLarge`);
2. detects its type from the content, reconciled with the extension of the uploaded file name - a file whose
   content contradicts its extension's kind is refused (`Dignite.Abp.BlobStoring:ContentTypeMismatch`), a
   picture named with another picture's extension is recorded as what it is;
3. hashes it (SHA-256);
4. dedups by content: when the same bytes are already stored in that container (same tenant), the new
   descriptor only refers to that blob and nothing is written;
5. otherwise saves the blob under a new random name (32 hex digits plus the canonical extension of the
   detected type, e.g. `.png` for a picture uploaded as `photo.jpg` - never the uploader's own extension;
   blobs stored earlier have no extension) without overwriting - the contributors run here and may refuse it
   (`ContentTypeNotAllowed`, the Imaging codes) - then writes the row, deleting the blob again if the row
   cannot be written.

The extension on the blob name is what lets the contributors promote a compound file named `.doc` to
`application/msword`, and give an empty file its extension's type.

The row records the detected MIME type, the hash of the **upload** (so the same original dedups even when
the container shrank it) and the size of what is **stored**. Two simultaneous uploads of the same bytes can
both miss the dedup lookup; a unique index on (tenant, container, hash) over live rows lets only one insert
win, and the other upload fails and can be retried, when it becomes a reference.

The file field's registration key `FileExplorer` and its configuration keys
`FileExplorer.FileContainerName` / `FileExplorer.UploadFileMultiple` keep the names they had in
abp-modules: they are stored in every field definition (Matrix block configurations included).

## The public read endpoint

`?Width=&Height=` is provider-independent: `ImageDimensionReader` (Site's own, because the Imaging package's
header reader is internal) guards the source dimensions before the provider decodes anything, and a provider
that answers Unsupported (SkiaSharp does not resize GIF) gets the file served as stored rather than an error.

The `ETag` is the blob name plus the requested size: blob names are generated and never reused, so the bytes
behind an address never change. The cache lifetime is a day rather than `immutable` because a file can be
deleted.

A public web app that calls Site through `Dignite.Site.Public.HttpApi.Client` depends on
`SitePublicHttpApiModule` so that its `FilePublicController` replaces the generated proxy controller for the
same interface (ABP removes a proxy controller whose interface a real controller implements), answers
`If-None-Match` with `304` and sets `ETag`/`Cache-Control` itself, and fetches the bytes through the client
proxy.

## Relative addresses

Since the release after 0.1.0-preview.25 a file address is relative: `SiteFileUrl.Build` composes
`/api/site-public/files/{container}/{blob}?__tenant=` with no scheme or host, and the application service's
mapper puts it on `FileDescriptorDto.Url` - so the admin API, the MCP tools and an in-process caller all hand
out the same address, whichever host the request came in on. It is what CmsKit does with its media
(`MediaDescriptorDto` carries no URL; the editor inserts `/api/cms-kit/media/{id}`, and pages render it
relative).

- `og:image`: `HeadMetadataBuilder.ReadOgImage` absolutizes against `SiteUrlContext.BaseUrl`
  (`SiteFileUrl.Absolutize`), the origin the canonical URL already uses; with no primary domain configured
  the head metadata fails the same way canonical does (`PrimaryDomainNotConfiguredException`, unless the host
  adds a request fallback).
- The admin UI: `SiteFileUrlService` resolves against `environment.apis.SiteAdmin.url`.
- Feeds, `llms.txt` and the sitemap carry no file addresses (a feed item's summary is text), so nothing else
  needs one made absolute.

## The admin UI's CKEditor field

CKEditor would load the images in its markup from the admin UI's own origin, where a relative
`/api/site-public/files/` address does not resolve. `provideSite()` registers `siteCKEditorConfigContributor`
under `@dignite/ng.flex-fields-ckeditor`'s `CKEDITOR_CONFIG_CONTRIBUTORS` (from `10.0.0-rc.26`). Its plugin
keeps the editor's **model** on the `SiteAdmin` API's host and only the editor's **data** relative, for the
`src` of an `imageBlock`/`imageInline`:

- upcast (`setData()`, the initial value, a paste): a `src` starting with `/api/site-public/files/` gets the
  API's host in front - a listener on `element:img` at `low` priority, after the image plugin's own converter
  has created the model image;
- data downcast (`getData()`: the form value that is saved, the Source view, a copy): a `src` on the API's
  host loses it again - a listener on `attribute:src:imageBlock|imageInline` at `high` priority that consumes
  the change, so the image plugin's converter skips it in the data pipeline only.

Everything else CKEditor does with the model's `src` then works unchanged: the editing view shows the image,
and an uploaded image's `width`/`height` are read from it. The upload provider (`SiteCKEditorUploadProvider`,
registered as `CKEDITOR_UPLOAD_PROVIDER`, since `@dignite/ng.flex-fields-ckeditor` `10.0.0-rc.25` ships no
uploader) hands the editor the uploaded file's address on the API's host for the same reason
(`SiteFileUrlService.toDisplay`). HTML and Markdown fields alike, since both load images into the same model;
links (`<a href>`) are not converted.

The read-only rich-text view (`ff-ckeditor-view`, e.g. a content list cell) has no editor, so it is handled
separately: `siteCKEditorDisplayContributor`, registered by `provideSite()` under
`CKEDITOR_DISPLAY_CONTRIBUTORS` (from `@dignite/ng.flex-fields-ckeditor` `10.0.0-rc.27`), receives the HTML the
view is about to render (after Markdown has been turned into HTML, before the sanitizer) and puts the
`SiteAdmin` API's host in front of every `src="/api/site-public/files/..."`, double- or single-quoted. Only
`src` is touched, not `href`, and external addresses stay as they are. The result is shown, never saved or
handed back, so nothing stored carries the host; when the UI shares its API's origin the HTML is returned
unchanged.

## MCP

The default container list and its model-facing descriptions are set in `SiteMcpModule`.
`SiteMcpFileOptions.GetMaxUploadSize` is the one rule both `site_upload_file` and `site_list_file_containers`
use, so what is advertised is what is enforced; `SiteMcpFileServerOptionsSetup` raises the MCP endpoint's
request-body limit, when it is lower, to just fit a base64-encoded file of `MaxUploadSize`.
