# Site files: internals

How Site uses the Drive for its files. What it does - the containers, the permissions, the addresses, serving, the admin
UI, MCP and the upgrade - is documented for users in [docs/en/files.md](../docs/en/files.md); this page covers what a
contributor needs on top of that. The decisions behind it are in abp-modules' `drive/dev-docs/drive-decision.md` (the
amendment of 2026-11-11) and in [drive-integration-plan.md](drive-integration-plan.md).

Until 3.0 Site kept its own file library (File Explorer before 0.1.0-preview.25, then Site's `Files/`): descriptors,
directories, an admin API, a picker, nine MCP tools. Since 3.0 the files are the open Drive's, and Site keeps only what is
Site's: its address, what it does on read, and how its fields use files.

## Where things live

| Piece | Location |
|---|---|
| Container names Site's fields use by default | `SiteFileContainerNames` (Domain.Shared). No longer a boundary: a field may name any container the host declares. |
| The containers' rules | The Drive's presets, declared by the host (`Drive` section of `appsettings.json`). JPEG to WebP and the upright resize are `Dignite.Abp.BlobStoring.Imaging.SkiaSharp`'s, used by the Drive's `Images` preset. |
| Site's address | `SiteFileUrl` (Domain.Shared): `Build`, `Normalize` (the Drive's address to Site's), `Sized`, `Absolutize`, `IsSiteFile`. The Angular mirror is `site-file-url.ts`. |
| Public read | `FilePublicAppService` (Public.Application) over `IDrivePublicAppService`; `FilePublicController` (Public.HttpApi) unchanged. |
| Legacy descriptors | `FileDescriptor`, `DirectoryDescriptor` and their repositories (Domain, EF Core, MongoDB), read only; removed in the next major version. |
| Migration | `Dignite.Site.Drive.Migration`: `SiteDriveMigrator`, run by a data seed contributor. |
| Admin UI | `@dignite/ng.site`: the file field's control and the SEO image inputs over `drive-file-input`, `SiteCKEditorUploadProvider`, `SiteUploadCleanup`. |
| MCP | None of Site's own; `SiteMcpConsts.FileInstructions` tells a client how Site's fields use the `drive_*` tools. |

Site's packages reference only `Dignite.Drive.Application.Contracts` (Public.Application) and `@dignite/ng.drive`. Only
the migration package references `Dignite.Drive.Domain`, because it writes Drive rows in process.

## The public read

`FilePublicAppService.GetAsync` finds the file as a `SiteStoredFile` - container, the id of the stored bytes (the
physical BLOB name), name, type and a way to open the bytes - from the Drive first, then from a legacy descriptor:

- **The Drive.** `IDrivePublicAppService.ReadAsync(container, blob, no size, HeadersOnly)` answers for a file of a
  `PublicRead` container whose physical BLOB name is `blob`. The bytes are read with a second call only when they are
  needed, so a rendition already in `RenderedImageCache` costs one lookup. The Drive's own sizing is not used: Site
  renders from the stored bytes, at any size, in the format the request takes.
- **The legacy library**, for a file not yet migrated, in `SiteFileContainerNames.All` only. The fallback goes with the
  legacy tables.

What follows - planning the rendition, the decode bounds, `RenderedImageCache`, the ETag and `Vary` in the controller -
is as it was. A remote Drive (`Dignite.Drive.HttpApi.Client`) reports a missing file as a remote call error rather than
`EntityNotFoundException`; only the in-process case is handled and tested today.

## Why the address survives the move

The Drive names a user drive's physical BLOB `{32 hex}{extension of the stored type}`, the form Site's own names had, and
its public read finds a user-drive file by that name. The migration gives each Drive row the legacy file's own BLOB name
and id, and copies a dedup reference's bytes to its own name, so `api/site-public/files/{container}/{blob}` finds every
file it found before and a field's `id` names the same file. Containers keep their names and their storage provider, so
an owner's bytes are not moved at all.

## Deleting what a form no longer holds

`drive-file-input` only takes a removed file out of the value. `SiteUploadCleanup`, provided by the content editor and
the SEO settings, records the files the current user uploaded during the edit (creator and creation time, since the
input reports no upload event) and, after a successful save, deletes those the saved value no longer names, by id or by
BLOB name. A file that was in the form before is never deleted: another content or language may use it. Leaving without
saving deletes nothing yet.

## Tests

- `FilePublicAppService_Tests` uploads through the Drive's `FileDescriptorManager` (so the `Images` preset runs) and
  keeps two tests for the legacy fallback; `LegacySiteFiles` writes legacy rows as the library did, a JPEG in `images`
  as upright WebP.
- `SiteDriveMigrator_Tests` covers the dry run, owners, references, an orphaned blob, the dedup key and a second run.
- The EF Core test module installs the Drive as the dev Host does; `common.test.props` reads
  `DigniteAbpModulesVersion` from `common.props`.
