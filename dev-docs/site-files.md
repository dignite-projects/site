# Site files: internals

How Site uses Blob Management for its files. What it does - the containers, the permissions, the upload, the addresses,
serving, the admin UI and MCP - is documented for users in [docs/en/files.md](../docs/en/files.md); this
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
| Admin UI | `@dignite/ng.site`: `SiteFileInputComponent`, `SiteFileUploadService`, `SiteUploadCleanup`, `SiteCKEditorUploadProvider`, the file field's control and the SEO image inputs; `SiteFilePickerModalComponent` and `SiteDirectoryTreeComponent` for the `Library` input mode, over `SiteBlobContainerService` and `SiteBlobDirectoryService` (Blob Management's container and directory endpoints, written by hand). The proxy `FileAdminService` is written by hand in the style of the generated ones. |
| Management page | The dev host (`angular/`) installs `@dignite/ng.blob-management` - usage, containers, directories and files, read only - under Administration > Blob Management (`provideBlobManagementConfig()`, the `blob-management` route). The package is not on any registry yet, so `angular/package.json` points at a tarball of abp-modules' build in `../../_local-feed`; point it at the published version before the branch is pushed. Seeing it takes `BlobManagement.Containers`, which the data seed grants the admin role. The tarball is newer than the `10.7.7-preview.1` server package, which lacks six of the page's texts (`Menu:Files`, `Usage:Tenants`, `Usage:Host`, `Usage:Allocation`, `Usage:Refresh`, `Permission:TenantUsages`); the host adds them to Blob Management's resource from `host/Dignite.Site.Host/Localization/BlobManagementPreview/` (en, zh-Hans) - remove them with the server package that carries them. |
| MCP | `FileTools` (Mcp) over `IFileAdminAppService`; `SiteMcpConsts.FileInstructions` tells a client how Site's fields use them. |

Site's packages reference only `Dignite.Abp.BlobManagement.Application.Contracts` (Admin.Application, Public.Application).
Site keeps no file table of its own: the host's migration `Site_RemoveLegacyFileLibrary` dropped `SiteFileDescriptors` and
`SiteDirectoryDescriptors`, and the migrator that moved their rows into Blob Management
(`Dignite.Site.BlobManagement.Migration`) went with them; it is in the history before that commit.

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
physical BLOB name), name, type and a way to open the bytes - in Blob Management, and a file it does not have is not
found:
`IBlobManagementPublicAppService.ReadAsync(container, blob, no size, HeadersOnly)` answers for a public file whose
physical BLOB name is `blob`. The bytes are read with a second call only when they are needed, so a rendition already in
`RenderedImageCache` costs one lookup. Blob Management's own sizing is not used: Site renders from the stored bytes, at
any size, in the format the request takes.

What follows - planning the rendition, the decode bounds, `RenderedImageCache`, the ETag and `Vary` in the controller -
is as it was.

**Blob Management in another service.** The admin side works unchanged: `IBlobManagementFileService` and
`IBlobManagementContainerAppService` are Contracts interfaces, and `Dignite.Abp.BlobManagement.HttpApi.Client` replaces
the in-process implementation of the first with `RemoteBlobManagementFileService`; the host swaps the module. The public
read does not: `IBlobManagementPublicAppService` has no remote implementation, because Blob Management's public read is
an address (`BlobManagementPublicController` is left out of the API definition, so no client proxy is generated), so
`FilePublicAppService` cannot resolve it in a host without Blob Management in process. It also needs a missing file to be
`EntityNotFoundException`, which a remote call does not raise; only the in-process case is handled and tested today.
Blob Management's public read is rate limited per client address, which behind Site would be Site's.

## The address of a file

Blob Management names the physical BLOB of a file kept by id `{32 hex}{extension of the stored type}`, and its public
read finds a file by that name, so `api/site-public/files/{container}/{blob}` is `{container}/{blob}` of Blob Management's
`/blobs/` address, and `SiteFileUrl.Normalize` turns one into the other.

## The file picker and the directories

A file field's `FileExplorer.InputMode` (`FileInputMode`: `Upload` = 0, `Library` = 1; an enum key of the field type, stored as its number, as the date field's `DateTime.InputMode` is) decides what the button of `site-file-input` does. In `Library` mode it opens `site-file-picker-modal`: the files of one directory, a page at a time, searchable, selectable across pages, with an upload button that files into the directory shown, and - beside the list - `site-directory-tree` when the container has directories and the user may see them. The window is built new; the old library's picker, modal and tree (before `4dbc05d`) carried drag and drop, a "my files" root and rename and move of files, none of which this needs.

Who answers what, and why:

- **Files** come from `IFileAdminAppService.GetListAsync` (`directoryId` added), so Site's rule for the container applies, as for every other file call.
- **Directories and the container's limits** come from Blob Management's own endpoints (`api/blob-management/directories`, `api/blob-management/containers/{name}`), called by the browser with the user's token. Blob Management authorizes the tree by grants on the container (`BlobManagement.Container.View`/`CreateDirectories`/`UpdateDirectories`/`DeleteDirectories`), and it deliberately has no trusted path for an application to write directories (`positioning-decision.md`, *Not chosen*), so a Site endpoint in front of it would add nothing. It would also fail in the one deployment where Blob Management is remote: the client-credentials token Site's back end calls with carries no user, which the directory API refuses.
- **What the user may do** is the `access` of the container (`GET api/blob-management/containers/{name}`, any signed-in user): the tree is shown when `allowDirectories` and `access.view`, and each button needs its own flag. `view` is true for a holder of `BlobManagement.Containers`; the three that write are never implied by it, so a role has to be granted them. `SiteFileDirectoryPermissionsDataSeedContributor` does it for `admin` in the dev host.
- **An upload's `directoryId`** is passed to Blob Management's ticket, which checks that the directory belongs to the container and that the container allows directories. Site does not ask whether the user may see that directory: the file service is a trusted caller and files are not hidden by their directory (`positioning-decision.md`).

Not done: moving a directory (Blob Management's page does, by dragging), renaming or moving files, deleting files from the window, and filing the legacy library's `Site:DirectoryPath` into the directories (`DirectoryPathImporter`).

## Deleting what a form no longer holds

`site-file-input` only takes a removed file out of the value. `SiteUploadCleanup`, provided by the content editor and the
SEO settings, records each file the input uploaded during the edit and, after a successful save, deletes those the saved
value no longer names, by id or by BLOB name. A file that was in the form before is never deleted: another content or
language may use it. Leaving without saving deletes nothing yet.

## Tests

- `FileAdminAppService_Tests` and `FileTools_Tests` run the whole upload - ticket, Blob Management's upload service,
  confirmation - in process, with the principal `AsEditorAsync` sets: the test base has no current user.
- `FilePublicAppService_Tests` uploads through Blob Management's `FileDescriptorManager` and marks the file public (so the
  `Images` preset runs).
- The EF Core test module installs Blob Management as the dev Host does; `common.test.props` reads
  `DigniteAbpModulesVersion` from `common.props`.
