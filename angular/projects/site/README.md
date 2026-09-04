# @dignite/ng.site

The Angular library for Dignite Site's admin UI: routes, components, and services under `src/`
(imported as `@dignite/ng.site`), plus `provideSite()` and friends under `config/`
(imported as `@dignite/ng.site/config`).

## Installing

```bash
npm install @dignite/ng.site
```

(Pre-release builds are published to GitHub Packages as `@dignite-projects/ng.site`; see the root
[README](../../README.md) / `angular/.npmrc` for the registry setup, and alias the dependency:
`"@dignite/ng.site": "npm:@dignite-projects/ng.site@<version>"`.)

### CKEditor and File Explorer dependencies

`provideSite()` (from `@dignite/ng.site/config`) unconditionally registers the CKEditor and File
Explorer flex-fields field types - it's not optional, skipping either one makes
`FieldTypeResolver.get(...)` throw and the content editor fails to render entirely. So this package
declares `@dignite/ng.flex-fields-ckeditor` and `@dignite/ng.flex-fields-file-explorer` as its own
`dependencies`, not `peerDependencies` - and from `10.0.0-rc.13` those two adapters in turn declare
everything *they* need (`@ckeditor/ckeditor5-angular`, `ckeditor5`, `marked`,
`@dignite/ng.file-explorer`) as their own dependencies rather than peers. `npm install
@dignite/ng.site` (see "Installing" above) is the whole install; there's nothing to add by hand.

That `>= 10.0.0-rc.13` floor is load-bearing, not cosmetic. Below `rc.12` those four packages were
the adapters' `peerDependencies`, and every ABP 10.5 + Angular 21 host installs with
`--legacy-peer-deps` (`@abp/ng.theme.shared` pins `@swimlane/ngx-datatable@~22`, whose own Angular
peer range stops at 20), under which npm does not install peers at all. Loosening the floor back
below `rc.12` would silently reintroduce four unresolvable imports - discovered at the consuming
app's build, with nothing in `npm install` to warn about them.

During the pre-release (GitHub Packages) channel you do not need to separately alias the
`@dignite/*` packages this one depends on (`@dignite/ng.flex-fields` and the two adapters above) -
the release workflow's scope-swap step rewrites every `@dignite/*` entry under `dependencies` in
the published package.json to the same alias form used for `@dignite/ng.site` itself above. That
step never covered `peerDependencies`, which is exactly why the old manual alias instruction lived
here.

### Required global styles (ng-zorro-antd)

`@dignite/ng.site` builds several fields on `ng-zorro-antd` (`nz-tree`, `nz-tree-select`, `nz-select`,
`nz-autocomplete` - pulled in directly or via the `@dignite/ng.flex-fields` dependency). These
components render their dropdown/panel content through Angular CDK's overlay, which attaches
directly to `<body>` outside this library's view tree - so they can only be styled with global CSS,
never with the scoped component styles an Angular library ships to consumers.

That makes this a hard limitation, not an oversight: ng-packagr has no mechanism to add entries to
a *consuming application's* `angular.json` (or global style bundle), so every app that installs
`@dignite/ng.site` has to register these four stylesheets itself. Skipping this doesn't break the
build - it silently leaves the affected controls unstyled (e.g. a tree-select panel with no
background or positioning). This has already caught out two separate hosts (this repo's own `Host`
app and the `cloud` project both needed the identical fix) - budget for it on every new one.

Add to the host app's `angular.json`, in `projects.<app>.architect.build.options.styles`:

```jsonc
{ "input": "node_modules/ng-zorro-antd/tree/style/index.min.css", "inject": true, "bundleName": "ng-zorro-antd-tree" },
{ "input": "node_modules/ng-zorro-antd/tree-select/style/index.min.css", "inject": true, "bundleName": "ng-zorro-antd-tree-select" },
{ "input": "node_modules/ng-zorro-antd/select/style/index.min.css", "inject": true, "bundleName": "ng-zorro-antd-select" },
{ "input": "node_modules/ng-zorro-antd/auto-complete/style/index.min.css", "inject": true, "bundleName": "ng-zorro-antd-auto-complete" }
```

If the host doesn't wire global styles through `angular.json`, `@import` the same four paths from
its root stylesheet instead.

`ng-zorro-antd` itself is declared as a direct `dependency` of `@dignite/ng.site` (not a peer), the
same way `@angular/cdk` is: every consumer needs it unconditionally, there's no opt-out the way
there is for the CKEditor/File Explorer field types, so there's no reason to push the install step
onto the host. `@swimlane/ngx-datatable` is different - it's a `peerDependency` here because
`@abp/ng.theme.shared` already guarantees every ABP host has it.

This list only covers what `@dignite/ng.site` and `@dignite/ng.flex-fields` import as of this writing.
Re-derive it before trusting it on a new `ng-zorro-antd` version, or after either package adds a new
field type:

```bash
grep -rohE "ng-zorro-antd/[a-z-]+" node_modules/@dignite/ng.site node_modules/@dignite/ng.flex-fields | sort -u
```

## Code scaffolding

Run `ng generate component component-name --project site` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module --project site`.
> Note: Don't forget to add `--project site` or else it will be added to the default project in your `angular.json` file.

## Build

Run `ng build site` to build the project. The build artifacts will be stored in the `dist/` directory.

## Running unit tests

Run `ng test site` to execute the unit tests via [Vitest](https://vitest.dev).

## Further help

To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI Overview and Command Reference](https://angular.io/cli) page.
