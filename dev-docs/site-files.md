# Site files: internals

How Site uses Blob Management for its files. What it does - the containers, the permissions, the upload, the addresses,
serving, the admin UI, MCP and the upgrade - is documented for users in [docs/en/files.md](../docs/en/files.md); this
page covers what a contributor needs on top of that. The decisions behind it are in abp-modules'
`blob-management/dev-docs/` (`positioning-decision.md`, `business-system-integration-decision.md`,
`peer-comparison.md`, `upload-checksum-decision.md`) and, for how Site arrived here, in
[drive-integration-plan.md](drive-integration-plan.md) (superseded).

Until 3.0 Site kept its own file library (File Explorer before 0.1.0-preview.25, then Site's `Files/`): descriptors,
directories, an admin API, a picker, nine MCP tools. Since 3.0 the files are Blob Management's, and Site keeps only what
is Site's: its address, what it does on read, who may upload where, and how its fields use files.

## Where things live

| Piece | Location |
|---|---|
| Container names Site's fields use by default | `SiteFileContainerNames` (Domain.Shared). Also the boundary of the admin API and of the MCP tools: only these three are reachable. |
| The containers' rules | Blob Management's presets, declared by the host (`BlobManagement` section of `appsettings.json`). JPEG to WebP and the upright resize are `Dignite.Abp.BlobStoring.Imaging.SkiaSharp`'s, used by the `Images` preset. |
| Site's address | `SiteFileUrl` (Domain.Shared): `Build`, `Normalize` (Blob Management's `/blobs/...` to Site's), `Sized`, `Absolutize`, `IsSiteFile`. The Angular mirror is `site-file-url.ts`. |
| Public read | `FilePublicAppService` (Public.Application) over `IBlobManagementPublicAppService`; `FilePublicController` (Public.HttpApi) unchanged. |
| Who may upload where | `SiteFileAuthorization` (Admin.Application): the container to permissions table. |
| The admin API | `IFileAdminAppService` / `FileAdminAppService` (Admin.Application.Contracts, Admin.Application) over `IBlobManagementFileService` and `IBlobManagementContainerAppService`; `FileAdminController` (Admin.HttpApi); the C# client proxy and its manifest entry in `site-admin-generate-proxy.json`. |
| Legacy descriptors | `FileDescriptor`, `DirectoryDescriptor` and their repositories (Domain, EF Core, MongoDB), read only; removed in the next major version. |
| Migration | `Dignite.Site.BlobManagement.Migration`: `SiteBlobManagementMigrator`, run by a data seed contributor. |
| Admin UI | `@dignite/ng.site`: `SiteFileInputComponent`, `SiteFileUploadService`, `SiteUploadCleanup`, `SiteCKEditorUploadProvider`, the file field's control and the SEO image inputs. The proxy `FileAdminService` is written by hand in the style of the generated ones. |
| Management page | The dev host (`angular/`) installs `@dignite/ng.blob-management` - usage, containers, directories and files, read only - under Administration > Blob Management (`provideBlobManagementConfig()`, the `blob-management` route). The package is not on any registry yet, so `angular/package.json` points at a tarball of abp-modules' build in `../../_local-feed`; point it at the published version before the branch is pushed. Seeing it takes `BlobManagement.Containers`, which the data seed grants the admin role. The tarball is newer than the `10.7.7-preview.1` server package, which lacks six of the page's texts (`Menu:Files`, `Usage:Tenants`, `Usage:Host`, `Usage:Allocation`, `Usage:Refresh`, `Permission:TenantUsages`); the host adds them to Blob Management's resource from `host/Dignite.Site.Host/Localization/BlobManagementPreview/` (en, zh-Hans) - remove them with the server package that carries them. |
| MCP | `FileTools` (Mcp) over `IFileAdminAppService`; `SiteMcpConsts.FileInstructions` tells a client how Site's fields use them. |

Site's packages reference only `Dignite.Abp.BlobManagement.Application.Contracts` (Admin.Application, Public.Application).
Only the migration package references `Dignite.Abp.BlobManagement.Domain`, because it writes file rows in process.

## The upload

Blob Management has no API for a user to store a file: the business system asks for an upload ticket for its user, the
browser posts the file to Blob Management with it, and the business system confirms the file (`positioning-decision.md`,
`business-system-integration-decision.md`). Site does that in its own `IFileAdminAppService`:

- `CreateUploadTicketAsync` checks `SiteFileAuthorization.CheckUploadAsync` for the container and asks
  `IBlobManagementFileService.CreateUploadTicketAsync` for a ticket with `UploaderId = CurrentUser.Id`.
- `ConfirmAsync` refuses a pending file whose `CreatorId` is not the caller - the ticket recorded the uploader, and the
  file is not listed or readable until confirmed, so this is the only check that the file is the caller's - then
  confirms it and marks it public (`SetPublicAsync`), because the visitors of the site read every file of Site's
  containers. A file already confirmed and public is returned as it is, so confirming twice is harmless.
- A pending file nobody confirms is deleted by Blob Management after `BlobManagementUploadOptions.PendingLifetime`, a day.

The ticket's address is relative to the host that serves Blob Management. The admin UI posts to it through ABP's
`RestService` under the `SiteAdmin` API name, which is right while Blob Management is in the same process as the
administration API (Site's host); a deployment that sets `AbpBlobStoringBlobManagementOptions.PublicBaseUrl` or runs Blob
Management apart would get an absolute address, and `SiteFileUploadService` would have to post to it directly.

Not chosen: relaying the bytes through an endpoint of Site. It needs the body size limits, the form-binding bypass and the
type checks Site's own file library had to carry, all of which Blob Management does on its own endpoint.

## The public read

`FilePublicAppService.GetAsync` finds the file as a `SiteStoredFile` - container, the id of the stored bytes (the
physical BLOB name), name, type and a way to open the bytes - from Blob Management first, then from a legacy descriptor:

- **Blob Management.** `IBlobManagementPublicAppService.ReadAsync(container, blob, no size, HeadersOnly)` answers for a
  public file whose physical BLOB name is `blob`. The bytes are read with a second call only when they are needed, so a
  rendition already in `RenderedImageCache` costs one lookup. Blob Management's own sizing is not used: Site renders from
  the stored bytes, at any size, in the format the request takes.
- **The legacy library**, for a file not yet migrated, in `SiteFileContainerNames.All` only. The fallback goes with the
  legacy tables.

What follows - planning the rendition, the decode bounds, `RenderedImageCache`, the ETag and `Vary` in the controller -
is as it was. A remote Blob Management (`Dignite.Abp.BlobManagement.HttpApi.Client`) reports a missing file as a remote
call error rather than `EntityNotFoundException`; only the in-process case is handled and tested today.

## Why the address survives the move

Blob Management names the physical BLOB of a file kept by id `{32 hex}{extension of the stored type}`, the form Site's
own names had, and its public read finds a file by that name. The migration gives each file the legacy file's own BLOB
name and id, and copies a dedup reference's bytes to its own name - Blob Management shares no bytes between files - so
`api/site-public/files/{container}/{blob}` finds every file it found before and a field's `id` names the same file.
Containers keep their names and their storage provider, so an owner's bytes are not moved at all.

The migration writes the legacy file's SHA-256 (upper-case hex) into `Hash`: the `10.7.7-preview.1` descriptor requires
one. A release of Blob Management after it keeps a hash only for an upload verified against one, as base64; the
migration then stops writing it (`upload-checksum-decision.md`).

## Deleting what a form no longer holds

`site-file-input` only takes a removed file out of the value. `SiteUploadCleanup`, provided by the content editor and the
SEO settings, records each file the input uploaded during the edit and, after a successful save, deletes those the saved
value no longer names, by id or by BLOB name. A file that was in the form before is never deleted: another content or
language may use it. Leaving without saving deletes nothing yet.

## Tests

- `FileAdminAppService_Tests` and `FileTools_Tests` run the whole upload - ticket, Blob Management's upload service,
  confirmation - in process, with the principal `AsEditorAsync` sets: the test base has no current user.
- `FilePublicAppService_Tests` uploads through Blob Management's `FileDescriptorManager` and marks the file public (so the
  `Images` preset runs) and keeps two tests for the legacy fallback; `LegacySiteFiles` writes legacy rows as the library
  did, a JPEG in `images` as upright WebP.
- `SiteBlobManagementMigrator_Tests` covers the dry run, owners, references, an orphaned blob and a second run.
- The EF Core test module installs Blob Management as the dev Host does; `common.test.props` reads
  `DigniteAbpModulesVersion` from `common.props`.
