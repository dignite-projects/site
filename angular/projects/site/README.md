# @dignite/site

The Angular library for Dignite Site's admin UI: routes, components, and services under `src/`
(imported as `@dignite/site`), plus `provideSite()` and friends under `config/`
(imported as `@dignite/site/config`).

## Installing

```bash
npm install @dignite/site
```

(Pre-release builds are published to GitHub Packages as `@dignite-projects/site`; see the root
[README](../../README.md) / `angular/.npmrc` for the registry setup, and alias the dependency:
`"@dignite/site": "npm:@dignite-projects/site@<version>"`.)

### Peer dependencies

`provideSite()` (from `@dignite/site/config`) unconditionally registers the CKEditor and File
Explorer flex-fields field types - it's not optional, skipping either one makes
`FieldTypeResolver.get(...)` throw and the content editor fails to render entirely. That pulls in
two more `@dignite/*` libraries as direct `dependencies` of this package (installed automatically
alongside `@dignite/site`), which in turn each carry their own peer dependencies that **this
package does not install for you** and your app must add explicitly:

| Peer package | Required by | Why |
|---|---|---|
| `@ckeditor/ckeditor5-angular` `^11.2.0` | `@dignite/ng.flex-fields-ckeditor` | Angular integration for CKEditor 5 |
| `ckeditor5` `^48.0.0` | `@dignite/ng.flex-fields-ckeditor` | CKEditor 5 itself |
| `marked` `^18.0.0` | `@dignite/ng.flex-fields-ckeditor` | Markdown rendering for the rich-text field type |
| `@dignite/ng.file-explorer` `^10.0.0-rc.5` | `@dignite/ng.flex-fields-file-explorer` | File picker UI for the file-explorer field type |

```bash
npm install @ckeditor/ckeditor5-angular@^11.2.0 ckeditor5@^48.0.0 marked@^18.0.0 @dignite/ng.file-explorer@^10.0.0-rc.5
```

These are declared as `peerDependencies`, not bundled `dependencies`, on purpose: CKEditor's
bundle isn't small, and not every consumer wants it forced on them as a transitive dependency.
The trade-off is that your app must install this list itself - a missing entry surfaces as a
module-not-found error wherever your build resolves `@dignite/site/config`'s imports, not as an
`npm install` failure.

During the pre-release (GitHub Packages) channel, `@dignite/ng.file-explorer` isn't published
under its public npmjs name yet - alias it the same way as `@dignite/site` itself:
`"@dignite/ng.file-explorer": "npm:@dignite-projects/ng.file-explorer@<version>"`. The CKEditor
packages are ordinary public npm packages at any channel and need no aliasing.

## Code scaffolding

Run `ng generate component component-name --project site` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module --project site`.
> Note: Don't forget to add `--project site` or else it will be added to the default project in your `angular.json` file.

## Build

Run `ng build site` to build the project. The build artifacts will be stored in the `dist/` directory.

## Running unit tests

Run `ng test site` to execute the unit tests via [Vitest](https://vitest.dev).

## Further help

To get more help on the Angular CLI use `ng help` or go check out the [Angular CLI Overview and Command Reference](https://angular.io/cli) page.
