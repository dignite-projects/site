# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **`.github/workflows/ci.yml` - pull-request and main-branch verification.** Until now
  `release.yml` was the repository's only CI: nothing was built, tested or checked until someone
  pushed a version tag, so a failure was discovered on the release path with the tag already cut.
  The new workflow mirrors release.yml's verification steps and stops there - no packing, no
  publishing, no credentials. Two deliberate differences from release.yml: it also runs
  `Dignite.Site.Mcp.Tests` (the standing MCP-tool/DTO contract test, which release.yml never ran,
  and which is what catches an MCP tool's parameters drifting from the DTO it builds), and it
  installs the Angular workspace with `yarn install --frozen-lockfile` rather than
  `npm install --no-package-lock`. release.yml uses npm because Yarn Classic fetches a dependency
  whose lockfile entry carries a GitHub Packages `resolved` URL without attaching registry auth;
  no such entry remains now that every `@dignite/*` package resolves from public npmjs, so CI can
  verify the tree the committed lockfile actually describes - the one every developer installs.
  There is still no lint step, for the reason release.yml already documents (~14 pre-existing
  `ng lint site` violations).
- **`.github/scripts/check-angular-package-duplicates.mjs`, run by `ci.yml`**: fails when one
  `@dignite/*` package is installed more than once in the tree. Angular libraries register through
  module-scoped `new InjectionToken(...)` values, so two copies are two distinct DI keys and a
  provider registered against one is invisible to a consumer of the other - the field type is
  simply absent at runtime, with nothing failing at install or build time. This is the check that
  would have caught the `@dignite/ng.flex-fields` split described under Changed above, and it is
  why `ci.yml` installs with Yarn rather than npm: npm dedupes the exact graph Yarn Classic splits,
  so the same check after `npm install` would pass vacuously.

### Changed

- **BREAKING**: the Angular library is renamed `@dignite/site` -> `@dignite/ng.site` (the
  pre-release GitHub Packages mirror follows: `@dignite-projects/site` -> `@dignite-projects/ng.site`),
  matching the `@dignite/ng.*` convention every other Dignite Angular package already uses.
  Consumers update the dependency name and every import path (`@dignite/site` and
  `@dignite/site/config` both move to their `ng.site` equivalents). Neither name has ever been
  published to npmjs - only pre-release builds went out under the old name to GitHub Packages - so
  no stable version is orphaned by the rename.
- Reclassified `angular/projects/site/package.json`'s dependencies against a single rule: a
  package only belongs in `peerDependencies` if a consumer is guaranteed to already have it -
  everything else is a `dependency`. The reason this matters in practice: every ABP 10.5 +
  Angular 21 host installs with `--legacy-peer-deps` (`@abp/ng.theme.shared` pins
  `@swimlane/ngx-datatable@~22`, whose own Angular peer range stops at 20), and npm does not
  install peers at all under that flag - Yarn Classic never installed them either. A peer the
  host doesn't already carry is therefore not a documented extra install step, it's an
  unresolvable import the consumer only discovers at their own build. Concretely: `@angular/cdk`
  and `ng-zorro-antd` stay `dependencies` (they arrive via `@abp/ng.components`, which not every
  ABP host has, so a consumer needs this package to bring its own copy); `@swimlane/ngx-datatable`
  moves dependency -> peer (guaranteed by `@abp/ng.theme.shared`).
- Bumped `@dignite/ng.flex-fields`, `-ckeditor` and `-file-explorer` from `^10.0.0-rc.5` to
  `^10.0.0-rc.13`, and dropped `@ckeditor/ckeditor5-angular`, `ckeditor5`, `marked` and
  `@dignite/ng.file-explorer` from this package entirely. Those four were only ever here to work
  around the adapters declaring them as `peerDependencies`, which `--legacy-peer-deps` never
  installs; flex-fields `10.0.0-rc.13` declares them as its own adapters' `dependencies`, so they
  now arrive transitively. Raising the floor is part of the fix, not housekeeping - a consumer
  resolving to `rc.5` under the old `^10.0.0-rc.5` range would get neither the adapters'
  declarations nor this package's, i.e. exactly the four unresolvable imports this whole
  reclassification exists to prevent.
- The four `@dignite/*` packages the Host dev app pulled through the GitHub Packages alias
  (`npm:@dignite-projects/...@10.0.0-rc.5`) are now plain public npmjs dependencies at
  `^10.0.0-rc.13`; all four reached npmjs for the first time at `rc.11`/`rc.13`.
  `angular/.npmrc`'s GitHub Packages auth is now only needed to *publish* this repo's own
  pre-release package, not to install anything. The release workflow's scope-swap step is
  unaffected - `@dignite-projects/*` still mirrors every version on GitHub Packages.
- Pinned `@dignite/ng.flex-fields` and `@dignite/ng.file-explorer` to `10.0.0-rc.13` through a
  `resolutions` block in `angular/package.json`. flex-fields `10.0.0-rc.13`'s adapter packages
  still declare their siblings at `^10.0.0-rc.4`, while npmjs' `latest` dist-tag for those siblings
  points at `10.0.0-rc.11` (`rc.13` shipped under `next`). Yarn Classic - which this workspace uses -
  prefers the `latest`-tagged version for any range that admits it, so a plain install produced two
  copies: `rc.13` at the root for this library, `rc.11` nested under `@dignite/ng.flex-fields-ckeditor`
  and `-file-explorer` for the field types they register. That is not a wasted-bytes problem:
  `FLEX_FIELD_TYPES` is a module-scoped `new InjectionToken(...)`, so two copies are two distinct DI
  keys - `provideCKEditorFieldType()` would register into a token `FieldTypeResolver` never reads,
  and the content editor would throw at runtime exactly as if the field type had never been
  provided. npm dedupes the same graph to a single copy and is unaffected, which is also why the
  release workflow's npm-based `verify-packed-npm-install.sh` cannot catch it. Drop this block once
  the adapters declare their siblings at a matching version.
- **`release.yml` installs the Angular workspace with `yarn install --frozen-lockfile`** instead of
  `npm install --no-package-lock`, so the released artifact is built from the tree the committed
  lockfile describes - the same one `ci.yml` verifies and every developer installs. npm also does
  not honour Yarn's `resolutions`, so under npm the release resolved the duplicate-copy question
  independently of the pin meant to settle it. The duplicate-package check now runs on the release
  path too, which it could not usefully do under npm (npm dedupes what Yarn Classic splits).
  `--legacy-peer-deps` goes away with npm: Yarn Classic does not enforce peer ranges, which is all
  that flag was working around.
- **`registry-url` moves off the job's first `actions/setup-node` onto a second call placed after
  the last yarn command**, which is what makes the switch above safe. setup-node writes an
  `//registry.npmjs.org/:_authToken=${NODE_AUTH_TOKEN}` placeholder into a generated `.npmrc` that
  stays active for every later step in the job; Yarn Classic eagerly substitutes every env-var
  placeholder in its resolved config on every invocation, not just registry-touching ones, and
  throws when one is unset - and nothing sets `NODE_AUTH_TOKEN` until the npmjs publish step at the
  very end. The second call still runs before both publish steps, because it is what writes the
  `$NPM_CONFIG_USERCONFIG` they depend on, and deliberately omits `cache: yarn` (that input makes
  setup-node probe `yarn cache dir`, itself a yarn invocation). abp-modules hit the identical
  failure and fixed it the same way.
- **`yarn test` runs `ng test site` instead of `ng test Host`.** The Host app has no spec files at
  all, so the script failed outright with "No tests found matching the following patterns"; the
  library's 29 tests were only reachable by typing `ng test site`, which `release.yml` already ran
  but no documented local command did.

### Removed

- `ContentEditorComponent`'s global `::ng-deep :root` block remapping CKEditor 5's
  `--ck-color-base-*` tokens onto the host theme. `@dignite/ng.flex-fields-ckeditor`
  `10.0.0-rc.13` ships that bridge itself, with a longer fallback chain than this copy had (a
  LeptonX token -> the Bootstrap 5.3 token every ABP Angular theme defines -> CKEditor's own stock
  literal, so a non-ABP host sees no change at all) and two fixes this copy never had:
  `.ck-editor__editable_inline`'s fully transparent border/background, which left an empty
  Basic-mode field indistinguishable from blank space, and `--ck-content-font-color`. Upstream also
  maps the editor surface to `--lpx-card-bg` where this copy used `--lpx-content-bg` for both
  background *and* foreground, flattening the toolbar/canvas distinction CKEditor's own stock
  palette makes. Keeping a copy here would not have added anything and would have fought the
  upstream rule at equal `:root` specificity, decided by style injection order alone.

### Fixed

- **`dotnet restore Dignite.Site.slnx` failed with `NU1605`**, so the new CI workflow and the next
  tagged release alike would have stopped at their first .NET step.
  `Microsoft.Extensions.FileProviders.Embedded` was pinned at `10.0.9` by `Dignite.Site.Host`,
  `Dignite.Site.Domain.Shared` and `Dignite.Site.Public.Web`, while abp-modules raised its own
  centrally-managed version to `10.0.11`. Two projects here reach that package through a relative
  `ProjectReference` into abp-modules - Host via `Dignite.Abp.FlexFields.CKEditor`, Public.Web via
  `Dignite.Abp.FlexFields.Abstractions` - so the resolved graph became a downgrade, which
  `NU1605`-as-error rejects. All three pins move to `10.0.11`.

  Nothing noticed sooner because there was no CI and `release.yml` has not run since abp-modules
  made that change. Both workflows check out abp-modules' *default branch*, so this class of drift
  arrives with no commit on this side at all - the first CI run on a pull request that changed
  nothing in `src/` found it.
- The library declared `@abp/ng.oauth`, `@abp/ng.components`, and `@volo/abp.commercial.ng.ui` as
  `dependencies` without importing any of them anywhere in the source or the built bundle -
  `@volo/abp.commercial.ng.ui` in particular was forcing a commercial package onto every consumer
  for no reason. Conversely, the built bundle imports `@angular/common`, `@angular/core`,
  `@angular/forms`, `@angular/router`, `rxjs`, and `@ngx-validate/core` (the last one only shows up
  in the compiled output, not the TS source - Angular's partial compilation flattens
  `ThemeSharedModule`'s re-export straight to the directive's home package) without declaring any
  of them. All are now declared - the Angular/rxjs set and `@ngx-validate/core` as
  `peerDependencies`, matching the reclassification above. A new release-workflow step
  (`check-angular-package-deps.mjs`, ported from `abp-modules`) now fails the build if a future
  emitted import and `package.json` drift apart again, rather than waiting for a consumer to hit
  it first.

## [0.1.0-preview.9] - 2026-08-30

### Added

- `Dignite.Site.Host` seeds a small demo site on first run (`SiteContentDataSeedContributor`):
  Home, About, an Events page demonstrating an optional `{slug?}` route placeholder, a
  parent/child News section demonstrating a regex-constrained route placeholder, and a bilingual
  Contact page exercising the Seo preset field - idempotent by name/slug lookup, so it never
  touches an already-populated database.
- A standing MCP-tool/DTO contract test (`Dignite.Site.Mcp.Tests`) fails, with a specific
  message, whenever an MCP tool's parameters drift from the `Create`/`Update`/`List` DTO it
  builds - found by this exact class of drift letting `create_page`/`update_page` silently drop
  `template`/`contentTemplate` since those parameters were added.

### Changed

- `Page.Template` and `Page.ContentTemplate` are collapsed into one required `Template` field,
  used for both the page-list and content-detail render paths (the view itself already branched
  on `Model.Content`). A blank or misconfigured `Template` now throws the standard view-not-found
  error instead of silently falling back to `Default`. A migration backfills existing rows from
  whichever of the two columns was set before dropping `ContentTemplate` and tightening `Template`
  to `NOT NULL`.
- Tenant-scoped views now resolve from `/Tenants/{id}/...` instead of `/Site/{id}/...`
  (`TenantViewLocationExpander`), and a `Template` value may include a trailing `.cshtml` without
  breaking view resolution.

### Fixed

- The slug preview's placeholder scanner used a regex that could not tell a `{name:FORMAT}`
  token's own closing brace apart from a balanced one inside its `:REGEX` segment (e.g. `\d{4}`),
  truncating the preview mid-regex and leaking the remainder as literal text. Ported the server's
  brace-depth-aware scan.
- Two page routes that reduce to the same bare address (e.g. a non-slug month-index page and a
  regex-constrained, slug-bearing sibling both resolving to `/news`) resolved unpredictably,
  picking whichever candidate an ordinal text sort happened to try first. A route with no
  `{slug}` now wins as the more complete address, the same reasoning that already prefers a
  literal route over a template sharing its address, extended one level.

## [0.1.0-preview.8] - 2026-08-26

### Fixed

- Collapsing a Matrix block used `*ngIf` to hide its sub-field controls, which destroys them - and
  the shared `FieldTypeControlBase.ngOnDestroy()` removes a destroyed control from its parent
  `FormGroup`, wiping whatever was typed before Save ever ran. Table has no collapse feature and was
  unaffected. Switched to a CSS-based hide so the controls stay mounted (and registered) regardless
  of expand state.

- Save correctly disabled when a field's value violated its own required/min/max/maxlength rules,
  but nothing near the field said why - true for every flex field control, not just Table's numeric
  columns, since none of them render error text regardless of nesting depth. Each field now reads
  its own `FormControl` errors directly instead of depending on `ff-flex-field-control`'s mounted
  component to render them, since those leaf components live in the separately-published
  `@dignite/ng.flex-fields` package.

- `Seo`/`Matrix`/`Table` validate a value case-insensitively, so a client that writes a key in the
  wrong casing (e.g. an AI/MCP caller inferring `MetaTitle` from the C# source instead of the
  `metaTitle` wire convention) passed validation, but `ContentManager.SetFieldValuesAsync` then
  persisted whatever casing arrived byte-for-byte - silently unreadable to every downstream reader
  that expects camelCase, despite having saved without error. A new `INormalizesValue` re-derives
  the canonical camelCase shape before the value reaches the bag; an unparseable value is left
  untouched so `Validate`'s own error path is unaffected. Companion fix: Seo's four value keys are
  fixed by the C# type rather than admin-configured like Matrix's block types or Table's columns, so
  they had no way to appear in a field's own Configuration for an AI client to read -
  `IHasValueShape` now exposes them as a type-level fact through `list_field_types`/`FieldTypeDto`.

### Changed

- Matrix blocks now show a visible border; Table's "add row" control moved into the header, next to
  the per-row remove buttons.

## [0.1.0-preview.7] - 2026-08-18

### Fixed

- `SiteRenderController` set the resolved content culture on `CultureInfo.CurrentCulture` inside the
  async action method, but `CurrentCulture` is `AsyncLocal`-backed and an async method restores its
  caller's `ExecutionContext` on completion - the assignment never reached the view. Pages rendered
  with whatever culture `UseAbpRequestLocalization()` picked from the admin cookie / Accept-Language
  header instead of the culture in the URL. Now applied by a `CultureScopedViewResult` wrapper that
  sets the culture inside `ExecuteResultAsync`, the same async flow that actually renders the view,
  layout, partials, and tag helpers.

- `pages.component.ts`'s parent-page tree picker dropdown was still unthemed after `0.1.0-preview.6`:
  `nz-tree-select` concatenates its dropdown class into the same class string as `ant-select-dropdown`
  on one `<div>`, unlike `nz-select`, which puts it on the ancestor `.cdk-overlay-pane`. The existing
  rule, `.parent-picker-dropdown .ant-select-dropdown`, was a descendant selector asking for two
  classes that are actually on the same element, so it never matched. Corrected to the compound
  selector `.parent-picker-dropdown.ant-select-dropdown`.

- The CKEditor chrome dark-mode remap lived in `angular/src/styles.scss`, this repo's own local
  dev/test shell, which is never part of the published `@dignite/site` package and so had no effect
  on a real consuming host. Moved into `content-editor.component.ts` (which does ship with the
  library) as a global `::ng-deep :root` rule.

### Added

- The default content template (`Default.cshtml`) now shows the content's publish time below the
  page title.

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

[Unreleased]: https://github.com/dignite-projects/site/compare/v0.1.0-preview.9...HEAD
