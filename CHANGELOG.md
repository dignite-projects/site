# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-preview.6] - 2026-08-17

### Fixed

- `@dignite/site`'s own components (`pages`, `fields`, `content-types/field-arrangement`) import
  `ng-zorro-antd/tree`, `ng-zorro-antd/tree-select`, `ng-zorro-antd/select`, and
  `ng-zorro-antd/auto-complete` directly, and `@dignite/ng.flex-fields` imports `ng-zorro-antd/tree`
  and `ng-zorro-antd/select` too - but `ng-zorro-antd` itself was never declared anywhere in
  `angular/projects/site/package.json`. It happened to resolve anyway because `@abp/ng.components`
  and `@dignite/ng.flex-fields` both depend on it directly, so it was always present transitively;
  the moment either of those stopped declaring it, consumers would get a bare module-not-found
  error with no obvious link back to this package. Now declared as a direct `dependency`, at the
  same `~21.0.2` floor as the workspace's own `angular/package.json`.

### Added

- `angular/projects/site/README.md` now documents the four `ng-zorro-antd` component stylesheets
  (`tree`, `tree-select`, `select`, `auto-complete`) every host must register as global styles -
  `nz-tree`/`nz-tree-select`/`nz-select`/`nz-autocomplete` render their dropdown/panel content
  through Angular CDK's overlay, outside this library's view tree, so they can only be styled
  globally, and no Angular library can wire that into a consuming app's `angular.json` on its
  behalf. `Dignite.Site.Host` and Dignite.Cloud had each independently rediscovered and hand-added
  the same four entries; the README now spells out the exact `angular.json` snippet, plus a command
  to re-derive the list if either package's `ng-zorro-antd` usage changes.

## [0.1.0-preview.5] - 2026-08-17

### Fixed

- `angular/projects/site/config/src/providers/route.provider.ts` (the `@dignite/site/config`
  entry point) imports `provideCKEditorFieldType` from `@dignite/ng.flex-fields-ckeditor` and
  `provideFileExplorerFieldType` from `@dignite/ng.flex-fields-file-explorer` - both called
  unconditionally by `provideSite()` - but `angular/projects/site/package.json` only declared
  `@dignite/ng.flex-fields`. Any consumer that only installed `@dignite/site` and called
  `provideSite()` per the docs got a module-not-found error the moment a bundler resolved the
  compiled `dignite-site-config.mjs`'s real imports - confirmed while integrating this package
  into Dignite.Cloud. Both packages are now declared as direct `dependencies`, at the same
  `^10.0.0-rc.5` floor as `@dignite/ng.flex-fields`.

### Added

- `angular/projects/site/README.md` now documents the four peer dependencies these two packages
  require but don't install for you - `@ckeditor/ckeditor5-angular`, `ckeditor5`, `marked` (for
  the CKEditor field type) and `@dignite/ng.file-explorer` (for the File Explorer field type).
  Deliberately kept as `peerDependencies`, not bundled, so a consumer whose content types never
  use rich-text or file-explorer fields isn't forced to install CKEditor's sizeable bundle;
  skipping one of them surfaces as a module-not-found error rather than an `npm install` failure,
  so the README spells out the full list instead of leaving it to be reconstructed one
  `peerDependencies` field at a time, the way it was for the Dignite.Cloud integration.
- A GitHub Packages install verification gate (`.github/scripts/verify-packed-npm-install.sh`),
  the npm equivalent of the NuGet restore gate added in `0.1.0-preview.3`: after publishing,
  installs the just-published `@dignite-projects/site` in an isolated scratch project - exactly as
  a real consumer would, aliased as `@dignite/site` with its documented peers - then bundles a
  file that imports `provideSite`. `npm install` alone would not have caught this release's bug:
  the missing dependencies only broke a bundler actually resolving the compiled `.mjs`'s import
  graph, not `npm install` or a `tsc --noEmit` type-check (`provideSite()`'s rolled-up `.d.ts`
  only exposes the opaque `EnvironmentProviders` return type, so TypeScript never had a reason to
  load the field types' own declarations).

## [0.1.0-preview.4] - 2026-08-17

### Fixed

- All four `*.HttpApi.Client` modules (`Dignite.Site.Admin`, `.Common`, `.Public`, and the unified
  `Dignite.Site`) called `AddHttpClientProxies` - dynamic proxies, which resolve each method by
  fetching `/api/abp/api-definition` from the configured `BaseUrl` at call time. Behind an API
  gateway, a downstream service's `BaseUrl` is the gateway itself, and the gateway's
  `/api/abp/api-definition` aggregates a *different* service's action list (confirmed against
  Dignite.Cloud: the gateway's definition document had zero `site-public` entries). Every call
  through the gateway failed with `Volo.Abp.AbpException: Could not find remote action for method:
  ...`, forcing downstream services to point `BaseUrl` at Site directly and bypass the gateway.
  Switched all four modules to `AddStaticHttpClientProxies`, which uses the `ClientProxies/*.cs` /
  `*.Generated.cs` classes `abp generate-proxy` already produces for Admin and Public (`dotnet pack`
  included them in every prior release, but nothing ever registered them) - their routes are baked in
  at generation time, so no live api-definition lookup is needed.

### Added

- `Dignite.Site.Public.HttpApi.Client.Tests`: `StaticClientProxyRegistrationTests` resolves an app
  service by interface from the client-side container and asserts the concrete type is the generated
  `*ClientProxy`, not a Castle dynamic proxy - a regression back to `AddHttpClientProxies` now fails
  this test instead of only surfacing behind a gateway in production. `ClientProxyCoverageTests`
  asserts, for each of the four modules, that its `Application.Contracts` assembly's app service
  interfaces exactly match the interfaces its `HttpApi.Client` assembly has a generated proxy for -
  static registration silently drops any app service that is missing one.

## [0.1.0-preview.3] - 2026-08-17

### Fixed

- `angular/projects/site/package.json` still declared `"@dignite/ng.flex-fields": "^10.0.0-rc.4"`,
  even though flex-fields `rc.5` renamed its field-type registration keys (`TextEdit`→`Text`,
  `NumericEdit`→`Number`, `DateEdit`→`DateTime`, `Switch`→`Boolean`, `TreeView`→`Tree`) and this
  package's own code was already updated for that rename. The declared floor now reads
  `^10.0.0-rc.5`, so this package can no longer be installed alongside a `flex-fields` version whose
  field types it doesn't actually understand.

### Added

- A NuGet restore verification gate in the release workflow (`release.yml`): after packing, every
  produced `.nupkg` is restored in an isolated scratch project against this run's own `artifacts/`
  output, GitHub Packages, and nuget.org, before anything is pushed. `0.1.0-preview.2` shipped a
  NuGet dependency pinned to `Dignite.FileExplorer.Application.Contracts >= 10.0.0-rc.6` - a version
  that was never published, because abp-modules' workspace `<Version>` had already been bumped past
  its last release at pack time. `dotnet restore Dignite.Site.slnx` never caught this: Site's own
  build resolves that dependency through a `ProjectReference`, not the `PackageReference` a real
  consumer gets from the packed output. This gate restores what a consumer would actually install.

## [0.1.0-preview.2] - 2026-08-17

### Added

- HTTP controllers for the three SEO document app services - `RobotsDocumentPublicController`,
  `SitemapDocumentPublicController`, `FeedDocumentPublicController` - under `api/site-public/robots`,
  `api/site-public/sitemap` and `api/site-public/feed`, plus their generated
  `Dignite.Site.Public.HttpApi.Client` proxies. `IRobotsDocumentAppService`, `ISitemapDocumentAppService`
  and `IFeedDocumentAppService` were previously only called in-process from `Dignite.Site.Public.Web`; a
  deployment that runs the Application layer as a separate service now has an HTTP surface to reach them
  through.

### Fixed

- The pre-release GitHub Packages publish step now rewrites every `@dignite/*` dependency in the
  packed Angular library's `package.json` to the `npm:@dignite-projects/*` alias form, alongside
  the existing package `name` rewrite. `@dignite-projects/site@0.1.0-preview.1` still declared a
  dependency on `@dignite/ng.flex-fields` - the public npmjs name, which doesn't exist there
  pre-release (only `@dignite-projects/ng.flex-fields` on GitHub Packages does) - so `npm install`
  of that version 404'd on it. The stable/npmjs publish path is unaffected.

## [0.1.0-preview.1] - 2026-08-16

First preview release. Introduces the NuGet packaging pipeline for Dignite.Site: `src/`'s Domain,
Application, HttpApi (and their Admin / Common / Public sub-app slices), EntityFrameworkCore,
MongoDB, Mcp, and Installer projects are now packable and publish to GitHub Packages, so
downstream services can consume them via `PackageReference` instead of a cross-repository
`ProjectReference`. As a `0.y.z` pre-release the package surface may still change.

### Added

- NuGet packaging infrastructure: versioned `common.props`, a release GitHub Actions workflow, and
  this changelog.

[Unreleased]: https://github.com/dignite-projects/site/compare/v0.1.0-preview.5...HEAD
