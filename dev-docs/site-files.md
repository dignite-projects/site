# Site files: internals

How Site uses Blob Management for its files. What it does - the containers, the permissions, the upload, the addresses,
serving, the admin UI and MCP - is documented for users in [docs/en/files.md](../docs/en/files.md); this
page covers what a contributor needs on top of that. The decisions behind it are in abp-modules'
`blob-management/dev-docs/` (`positioning-decision.md`, `business-system-integration-decision.md`,
`peer-comparison.md`, `upload-checksum-decision.md`) and, for how Site arrived here, in
[drive-integration-plan.md](drive-integration-plan.md) (superseded).

Until 3.0 Site kept its own file library (File Explorer before 0.1.0-preview.25, then Site's `Files/`): descriptors,
directories, an admin API, a picker, nine MCP tools, and a read endpoint that resized and converted pictures. Since 3.0
the files are Blob Management's, a browser reads them from Blob Management, and Site keeps only what has a rule of
Site's: who may upload where, the file input, the MCP tools, and how its fields and templates use a file's address.
How Site arrived at reading from Blob Management directly is [site-files-direct-read-plan.md](site-files-direct-read-plan.md).

## Where things live

| Piece | Location |
|---|---|
| Container names Site's fields use by default | `SiteFileContainerNames` (Domain.Shared). Also the boundary of the admin API and of the MCP tools: only these three are reachable. |
| The containers' rules | Blob Management's presets, declared by the host in code (`SiteHostModule.ConfigureBlobStoring`); Blob Management reads no configuration section for its containers. JPEG to WebP and the upright resize are `Dignite.Abp.BlobStoring.Imaging.SkiaSharp`'s, used by the `Images` preset. |
| A file's address | Blob Management's `/blobs/{container}/{blob}[?__tenant=]`, the file's `url`, stored as it is. `SiteFileUrl` (Domain.Shared, over Blob Management's Domain.Shared): `Sized`, `Absolutize`, `IsSiteFile`; `SiteImageSizes` the sizes the host allows and `Sized` rounds to. The Angular mirror is `site-file-url.ts` and `SiteFileUrlService`. |
| Public read | Blob Management's, read by the browser. `FilePublicController` (Public.HttpApi) only redirects the addresses Site served before 3.0. |
| Who may upload where | `SiteFileAuthorization` (Admin.Application): the container to permissions table. |
| The admin API | `IFileAdminAppService` / `FileAdminAppService` (Admin.Application.Contracts, Admin.Application) over `IBlobManagementFileService` and `IBlobManagementContainerAppService`; `FileAdminController` (Admin.HttpApi); the C# client proxy and its manifest entry in `site-admin-generate-proxy.json`. |
| Admin UI | `@dignite/ng.site`: `SiteFileInputComponent`, `SiteFileUploadService`, `SiteUploadCleanup`, `SiteCKEditorUploadProvider`, the file field's control and the SEO image inputs; `SiteFilePickerModalComponent` and `SiteDirectoryTreeComponent` for the `Library` input mode, over `SiteBlobContainerService` and `SiteBlobDirectoryService` (Blob Management's container and directory endpoints, written by hand). The proxy `FileAdminService` is written by hand in the style of the generated ones. |
| Management page | The dev host (`angular/`) installs `@dignite/ng.blob-management` - usage, containers, directories and files, read only - under Administration > Blob Management (`provideBlobManagementConfig()`, the `blob-management` route). The package is published with abp-modules' pre-releases only, to GitHub Packages as `@dignite-projects/ng.blob-management`, so `angular/package.json` depends on it under that name and the two imports use it too: an `npm:` alias under `@dignite` would download with no credential in Yarn Classic, which picks a tarball's credential by the name it installs the package under. Go back to `@dignite/ng.blob-management` from npmjs with Blob Management's first stable release. Installing it takes a `read:packages` token for `//npm.pkg.github.com/` in the user-level `.npmrc`. Seeing it takes `BlobManagement.Containers`, which the data seed grants the admin role. |
| MCP | `FileTools` (Mcp) over `IFileAdminAppService`; `SiteMcpConsts.FileInstructions` tells a client how Site's fields use them. |

Site's packages reference Blob Management's contracts only: `Dignite.Abp.BlobManagement.Application.Contracts`
(Admin.Application) and `Dignite.Abp.BlobManagement.Domain.Shared` (Domain.Shared, for `BlobManagementPublicUrl`).
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
`RestService` under the `BlobManagement` API name (`BLOB_MANAGEMENT_API_NAME`), which ABP resolves to `apis.default`
while the environment does not list it - right when Blob Management is in the host of the administration API. The post
carries no permission check of Site's and needs none: the ticket is the credential, and Site issued it only after
`SiteFileAuthorization`. The MCP tools put the ticket's address on `SiteMcpOptions.BaseUrl` (or the request's host), so
the gateway in front of the MCP endpoint routes `/api/blob-management/` too.

Not chosen: relaying the bytes through an endpoint of Site. It needs the body size limits, the form-binding bypass and the
type checks Site's own file library had to carry, all of which Blob Management does on its own endpoint.

## The public read

A browser reads a file at Blob Management's public address, which is what Blob Management's design gives it for
(`business-system-integration-decision.md`: an address a page embeds and a CDN caches). Site is not in between: an
anonymous read of a public file has no rule of Site's to apply, and relaying it would need Blob Management's public read
as a remote service - which it is deliberately not (`BlobManagementPublicController` is left out of the API definition) -
and would put every visitor behind Site's one client address in Blob Management's rate limit.

What that costs, against the read endpoint Site had: no WebP for a browser that takes it beyond what the `Images` preset
stores, no JPEG for a client without WebP, and only the sizes the container allows. The first is mostly gone: the
`Images` preset stores a PNG as lossless WebP on upload (`StorePngAsWebp`), unless it is small, 16-bit or animated - the
size below which a PNG stays a PNG is Blob Management's to set. Blob Management converts on upload rather than on read,
so the second stays. The sizes are `SiteImageSizes`.

**Sizes.** `SiteImageSizes.All` is Blob Management's default list with `og:image`'s 1200 and 630; the dev host sets it on
Site's containers with `UseBlobManagementPublicSizes`, and a Blob Management that runs apart has to be given the same
list. `SiteFileUrl.Sized` rounds a size given alone up to it, which keeps the picture's proportions, and writes a pair as
given - rounding the two apart would change the box. The Angular `sizedFileUrl` rounds both, for the admin UI's square
thumbnails.

**Blob Management in another service.** `IBlobManagementFileService` and `IBlobManagementContainerAppService` are
Contracts interfaces, and `Dignite.Abp.BlobManagement.HttpApi.Client` replaces the in-process implementation of the first
with `RemoteBlobManagementFileService` and of the second with the generated client proxy; the host swaps the module and
nothing in Site changes. Not tried end to end yet. What reading the code turned up:

- Site's back end then calls Blob Management with a client-credentials token, which carries no user. Blob Management
  10.7.7-preview.2 answers `GET api/blob-management/containers/{name}` for users only ("a client reads a container's limits
  from its own configuration"), so `FileAdminAppService.GetContainersAsync` - `GET api/site-admin/files/containers` and the
  MCP tool `site_list_file_containers` - fails there, and so it does in process for an MCP client signed in with client
  credentials. The file input then offers an upload with no size or type check of its own (Blob Management still checks
  on upload), and the file field's configuration lists all three containers. Site has no configuration of its own to read
  the limits from, and keeping a copy of the presets in Site's host would drift from Blob Management's. Appwrite's server
  SDK reads a bucket's `maximumFileSize` and `allowedFileExtensions` with an API key (`getBucket`), and Supabase's
  `getBucket` returns `file_size_limit` and `allowed_mime_types`. So the fix is Blob Management's: one container's limits
  for any authenticated caller, a client's `access` being none - on the branch
  `fix/blob-management-container-limits-for-clients` of abp-modules, not released yet.
- The admin UI calls Blob Management's container and directory APIs itself, with the user's token (the file picker), so
  its token has to be one Blob Management accepts: `oAuthConfig.scope` names Blob Management's scope beside Site's.
- A site served by an application of its own (`Dignite.Site.Public.Web` with Site's data remote) answers neither
  `/blobs/` nor `/api/site-public/files/`, whether Blob Management runs apart or in the host of the administration API: its
  gateway routes both ([Reaching Blob Management from Every Site](../docs/en/files.md#reaching-blob-management-from-every-site)).

## The address of a file

Blob Management names the physical BLOB of a file kept by id `{32 hex}{extension of the stored type}`, and its public
read finds a file by that name. A file's `url` is `/blobs/{container}/{blob}`, with `?__tenant=` for a tenant's file;
Site stores it as it is and never rewrites it. An address Site served before 3.0, `api/site-public/files/{container}/{blob}`,
carries the same `{container}/{blob}`, so `FilePublicController` redirects it to `BlobManagementPublicUrl.Build` of the
request's tenant, with the size rounded as `Sized` rounds it.

Not chosen: an option for a separate file origin (a CDN host put in front of the address on output). CKEditor's HTML and
templates hold the relative address too, and Site does not rewrite HTML on render, so `/blobs/` has to answer on the
site's origin anyway; the option would cover only `og:image`, JSON-LD and `file_url`.

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
- `FileAdminAppService_Tests` also reads a confirmed file through Blob Management's public read in process, and a deleted
  one as not found.
- `FilePublicRedirectHttpTests` (Public.HttpApi.Client.Tests) runs the redirect of the earlier addresses over HTTP.
  `SiteFileUrl_Tests` and the Angular `site-file-url.spec.ts` cover the address rules.
- The EF Core test module installs Blob Management as the dev Host does; `common.test.props` reads
  `DigniteAbpModulesVersion` from `common.props`.
