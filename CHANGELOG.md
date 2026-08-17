# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[Unreleased]: https://github.com/dignite-projects/site/compare/v0.1.0-preview.2...HEAD
