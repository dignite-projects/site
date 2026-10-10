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

`api/site-public/files/{containerName}/{blobName}?__tenant={tenantId}` is the address the admin API and
the MCP tools hand out (`FileDescriptorDto.Url`) and what a field stores. It is anonymous - a published
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

The address is built on the host the request came in on. Behind a gateway that is the gateway's host,
which must therefore route `GET`/`HEAD` `/api/site-public/files/{**}` to the service running Site.

## CKEditor image upload

`@dignite/ng.flex-fields-ckeditor` (from `10.0.0-rc.25`) ships no uploader; `provideSite()` registers
`SiteCKEditorUploadProvider` as its `CKEDITOR_UPLOAD_PROVIDER`. An image inserted into a CKEditor field is
uploaded to `api/site-admin/files` into the field's `CKEditor.ImagesContainerName` (normally
`site-images`) and embedded by its public address.

## MCP

The file tools reach both Site containers by default (`SiteMcpFileOptions.Containers`, with a description
a model reads to pick one); a host can narrow the list. Uploads travel base64-encoded, so they are capped
at `SiteMcpFileOptions.MaxUploadSize` (5 MB by default), and the MCP endpoint's request-body limit is
raised to fit one.
