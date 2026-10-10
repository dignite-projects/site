# Site files

Site has its own file library: the images and attachments content fields point at, the share image of the
SEO field, and the images inserted into a CKEditor field. It used to be a separate module in abp-modules;
since 0.1.0-preview.25 it is part of Site, under Site's namespaces, tables,
permissions and routes (abp-modules keeps only the upload pipeline, `Dignite.Abp.FileStoring` - see its
`docs/core-only-decision.md` for why).

## Where things live

| Piece | Location |
|---|---|
| Descriptors and directories (`FileDescriptor`, `DirectoryDescriptor`, their managers and repositories) | `Dignite.Site.Domain` - `Files/`, `Directories/` |
| Tables | `SiteFileDescriptors`, `SiteDirectoryDescriptors` (EF Core); collections of the same names (MongoDB) |
| Containers | `SiteFileContainerNames`: `site-images` (raster images only, 10 MB) and `site-files` (documents and images, 20 MB); their upload rules are set in `SiteAdminApplicationModule`, a host only picks the storage provider |
| Admin API | `api/site-admin/files`, `api/site-admin/directories` (`IFileAdminAppService`, `IDirectoryAdminAppService`) |
| Public read | `GET`/`HEAD` `api/site-public/files/{containerName}/{blobName}` (`IFilePublicAppService`) |
| MCP tools | `site_list_file_containers`, `site_list_files`, `site_get_file`, `site_upload_file`, `site_update_file`, `site_delete_file`, `site_list_directories`, `site_create_directory` |
| File field type | `FileFieldType` in `Dignite.FlexFields.Site` (registration key `FileExplorer`), its view in `Dignite.Site.Public.Web/Views/Shared/FlexFields/` |
| Admin UI | `@dignite/ng.site`: the file picker, the browsing modal and the file field type (`src/lib/files`, `src/lib/field-types/file`) |

Bytes are stored through `IFileStorer` (abp-modules `Dignite.Abp.FileStoring`): it caps the upload while
copying, detects the type from the content (never the uploader's claim), runs the container's handlers
(size, allowed types, image resize) and saves under a generated name. `FileDescriptorManager` adds the
row, and dedups by content: the same bytes uploaded twice to one container keep one blob, the second
descriptor referring to the first one's.

The file field's registration key `FileExplorer` and its configuration keys
`FileExplorer.FileContainerName` / `FileExplorer.UploadFileMultiple` keep the names they had in
abp-modules: they are stored in every field definition (Matrix block configurations included).

## Permissions

There is no permission of the file library's own. Files ride on the Contents permissions, per container:

| Operation | Needs |
|---|---|
| Upload a file, create a directory | `SiteAdmin.Contents.Create` |
| Rename or move a file | `SiteAdmin.Contents.Update`, or being its uploader |
| Delete a file | `SiteAdmin.Contents.Delete`, or being its uploader |
| List and manage everyone's files | `SiteAdmin.Contents` (without it a user lists only their own) |
| Read a file | nobody - Site's files are public |

Directories are personal: each user files uploads in a tree of their own, and only that user may rename,
move or delete it. A directory operation needs a signed-in user; a client-credentials token is refused.

All admin services are behind the `Site.Enable` feature like the rest of the admin surface.

## The public read endpoint

`/api/site-public/files/{containerName}/{blobName}?__tenant={tenantId}` is the address the admin API and
the MCP tools hand out (`FileDescriptorDto.Url`) and what a field stores - relative, see
[Addresses](#addresses) below. It is anonymous - a published
site's images are for its visitors - and serves **only** `SiteFileContainerNames`: another container, a
deleted file and a name that does not exist are all `404`. The tenant comes from `__tenant`.

- `?Width=&Height=` resizes an image on the way out (both given: cropped to fill). `SiteFileUrl.Sized` in
  C# and `sizedFileUrl` in `@dignite/ng.site` build such addresses; templates call
  `SiteFileUrl.Sized(url, width, height)`.
- `api/site-public/files/download/{containerName}/{blobName}?fileName=` serves the file as an attachment.
- Responses carry `ETag` (the blob name plus the requested size: blob names are generated and never
  reused, so the bytes behind an address never change), `Cache-Control: public, max-age=86400` and
  `X-Content-Type-Options: nosniff`; a matching `If-None-Match` is answered `304` without reading the blob.
  The cache lifetime is a day rather than `immutable` because a file can be deleted.

## Addresses

Since the release after 0.1.0-preview.25 a file address is **relative**: `SiteFileUrl.Build` composes
`/api/site-public/files/{container}/{blob}?__tenant=` with no scheme or host, and the application service's
mapper puts it on `FileDescriptorDto.Url` - so the admin API, the MCP tools and an in-process caller all
hand out the same address, whichever host the request came in on. It is what CmsKit does with its media
(`MediaDescriptorDto` carries no URL; the editor inserts `/api/cms-kit/media/{id}`, and pages render it
relative). No host is written into content, so moving the public site, the API or a gateway to another
host leaves every stored address valid.

Who puts a host in front of it:

| Consumer | Resolves the address against |
|---|---|
| A page of the public site (`<img src>`, a template's `SiteFileUrl.Sized(...)`) | the page's own origin: the public web app serves `/api/site-public/files/` itself |
| `og:image` (`HeadMetadataBuilder.ReadOgImage`) | the site's primary domain, `SiteUrlContext.BaseUrl` (`SiteFileUrl.Absolutize`) - the origin the canonical URL already uses; with no primary domain configured the head metadata fails the same way canonical does (`PrimaryDomainNotConfiguredException`, unless the host adds a request fallback) |
| The admin UI (`@dignite/ng.site`) | the `SiteAdmin` API's host, `environment.apis.SiteAdmin.url` (`SiteFileUrlService`) |

Feeds, `llms.txt` and the sitemap carry no file addresses (a feed item's summary is text), so nothing else
needs one made absolute. An absolute address a user typed into a field - an external image - is left as it
is everywhere.

The public web app has to serve `/api/site-public/files/{**}` on its own origin. A host that runs Site
in-process (`SiteHttpApiModule`) does. A public web app that calls Site through
`Dignite.Site.Public.HttpApi.Client` should depend on `SitePublicHttpApiModule`: its `FilePublicController`
then replaces the generated proxy controller for the same interface (ABP removes a proxy controller whose
interface a real controller implements), answers `If-None-Match` with `304` and sets `ETag`/`Cache-Control`
itself, and fetches the bytes through the client proxy. Without it the proxy controller still answers, but
with no caching headers. A gateway in front of the admin API still routes `GET`/`HEAD`
`/api/site-public/files/{**}` to the service running Site, for the admin UI's previews.

**The admin UI's CKEditor field.** CKEditor would load the images in its markup from the admin UI's own
origin, where a relative `/api/site-public/files/` address does not resolve. `provideSite()` registers
`siteCKEditorConfigContributor` under `@dignite/ng.flex-fields-ckeditor`'s `CKEDITOR_CONFIG_CONTRIBUTORS`
(from `10.0.0-rc.26`). Its plugin keeps the editor's **model** on the `SiteAdmin` API's host and only the
editor's **data** relative, for the `src` of an `imageBlock`/`imageInline`:

- upcast (`setData()`, the initial value, a paste): a `src` starting with `/api/site-public/files/` gets the
  API's host in front - a listener on `element:img` at `low` priority, after the image plugin's own
  converter has created the model image;
- data downcast (`getData()`: the form value that is saved, the Source view, a copy): a `src` on the API's
  host loses it again - a listener on `attribute:src:imageBlock|imageInline` at `high` priority that
  consumes the change, so the image plugin's converter skips it in the data pipeline only.

Everything else CKEditor does with the model's `src` then works unchanged: the editing view shows the
image, and an uploaded image's `width`/`height` are read from it. The upload provider hands the editor the
uploaded file's address on the API's host for the same reason (`SiteFileUrlService.toDisplay`). HTML and
Markdown fields alike, since both load images into the same model; links (`<a href>`) are not converted.

Known gap: the read-only rich-text view (`ff-ckeditor-view`, e.g. a content list cell) renders the stored
HTML as is, so an image in it loads only when the UI shares its API's origin. The display hook it needs is
being added in `@dignite/ng.flex-fields-ckeditor` (abp-modules); Site will use it once released.

## CKEditor image upload

`@dignite/ng.flex-fields-ckeditor` (from `10.0.0-rc.25`) ships no uploader; `provideSite()` registers
`SiteCKEditorUploadProvider` as its `CKEDITOR_UPLOAD_PROVIDER`. An image inserted into a CKEditor field is
uploaded to `api/site-admin/files` into the field's `CKEditor.ImagesContainerName` (normally
`site-images`) and embedded by its public address - stored relative, see
[Addresses](#addresses) for how the editor works with it.

## MCP

The file tools reach both Site containers by default (`SiteMcpFileOptions.Containers`, with a description
a model reads to pick one); a host can narrow the list. Uploads travel base64-encoded, so they are capped
at `SiteMcpFileOptions.MaxUploadSize` (5 MB by default), and the MCP endpoint's request-body limit is
raised to fit one.
