# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[Unreleased]: https://github.com/dignite-projects/site/compare/v0.1.0-preview.3...HEAD
