# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-preview.1] - 2026-08-16

First preview release. Introduces the NuGet packaging pipeline for Dignite.Site: `src/`'s Domain,
Application, HttpApi (and their Admin / Common / Public sub-app slices), EntityFrameworkCore,
MongoDB, Mcp, and Installer projects are now packable and publish to GitHub Packages, so
downstream services can consume them via `PackageReference` instead of a cross-repository
`ProjectReference`. As a `0.y.z` pre-release the package surface may still change.

### Added

- NuGet packaging infrastructure: versioned `common.props`, a release GitHub Actions workflow, and
  this changelog.

[Unreleased]: https://github.com/dignite-projects/site/compare/v0.1.0-preview.1...HEAD
