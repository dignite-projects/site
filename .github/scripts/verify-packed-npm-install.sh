#!/usr/bin/env bash
# Installs the npm package this workflow just published, in an isolated scratch project pointed at
# the same source a real consumer would use (GitHub Packages, pre-release), then bundles a minimal
# file that imports `provideSite` from `@dignite/site/config`. Catches the class of bug that
# `npm install` alone cannot: a real ES import inside the published bundle (e.g.
# `import { provideCKEditorFieldType } from '@dignite/ng.flex-fields-ckeditor'` in
# fesm2022/dignite-site-config.mjs) pointing at a package that isn't declared as a dependency
# anywhere - install succeeds, and only a real bundler resolving that import graph fails. A
# `tsc --noEmit` type-check does not catch this either: ng-packagr's rolled-up `.d.ts` for
# `provideSite()` only exposes the opaque `EnvironmentProviders` return type, so TypeScript never
# needs to load the field types' own declaration files - only a bundler walking the actual .mjs
# import graph does. This is what caught `@dignite/ng.flex-fields-ckeditor` /
# `@dignite/ng.flex-fields-file-explorer` missing from angular/projects/site/package.json's
# `dependencies` in 0.1.0-preview.4 - see CHANGELOG.md's 0.1.0-preview.5 entry.
#
# Installs `--legacy-peer-deps`, matching the "Install Angular dependencies" step earlier in this
# same job (this workspace already relies on that leniency for an existing ABP/@angular version
# mismatch - see that step's own comment).
#
# The Angular/rxjs/tslib baseline below is not something this package declares (a published
# Angular library assumes its host app supplies these, the same way `@dignite/site` doesn't
# declare them either) - it stands in for "a real Angular host app", which always has them.
set -euo pipefail

version=${1:?Usage: verify-packed-npm-install.sh <version> <github-token>}
github_token=${2:?Usage: verify-packed-npm-install.sh <version> <github-token>}

workdir=$(mktemp -d)
trap 'rm -rf "$workdir"' EXIT

cat > "$workdir/.npmrc" <<EOF
@dignite-projects:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${github_token}
EOF

# @dignite/site itself is aliased from the GitHub Packages name, exactly as a real consumer would
# declare it (see angular/projects/site/README.md's "Installing" section and
# apps/angular/package.json in Dignite.Cloud). The four peer packages
# (@ckeditor/ckeditor5-angular, ckeditor5, marked, @dignite/ng.file-explorer) are the ones that
# README documents as required but not bundled - @dignite/ng.file-explorer needs the same alias
# treatment pre-release, for the same reason.
cat > "$workdir/package.json" <<EOF
{
  "name": "verify-npm-install-scratch",
  "private": true,
  "dependencies": {
    "@dignite/site": "npm:@dignite-projects/site@${version}",
    "@dignite/ng.file-explorer": "npm:@dignite-projects/ng.file-explorer@10.0.0-rc.5",
    "@ckeditor/ckeditor5-angular": "^11.2.0",
    "ckeditor5": "^48.0.0",
    "marked": "^18.0.0",
    "@angular/core": "~21.2.0",
    "@angular/common": "~21.2.0",
    "@angular/forms": "~21.2.0",
    "@angular/cdk": "~21.2.0",
    "@angular/router": "~21.2.0",
    "@angular/platform-browser": "~21.2.0",
    "@angular/animations": "~21.2.0",
    "rxjs": "~7.8.0",
    "tslib": "^2.1.0"
  }
}
EOF

echo "Installing @dignite-projects/site@${version} (aliased as @dignite/site) from GitHub Packages, plus its documented peer dependencies..."
if ! (cd "$workdir" && npm install --no-audit --no-fund --legacy-peer-deps); then
  echo "::error::npm install of @dignite-projects/site@${version} failed - a declared dependency doesn't resolve. Do not publish this as the released version."
  exit 1
fi

cat > "$workdir/smoke.ts" <<'EOF'
import { provideSite } from '@dignite/site/config';

export const providers = provideSite();
EOF

echo "Type-checking a minimal consumer of provideSite()..."
if ! (cd "$workdir" && npx --yes -p typescript@~5.9.0 tsc --noEmit --strict --module esnext --moduleResolution bundler --target es2022 --skipLibCheck smoke.ts); then
  echo "::error::Type-checking 'import { provideSite } from \"@dignite/site/config\"' failed."
  exit 1
fi

echo "Bundling the same file to prove every real import it pulls in actually resolves..."
if ! (cd "$workdir" && npx --yes esbuild smoke.ts --bundle --platform=browser --format=esm --outfile=out.js); then
  echo "::error::Bundling a minimal consumer of provideSite() failed - an import inside the published package (directly or transitively) doesn't resolve. Do not publish this as the released version."
  exit 1
fi

echo "@dignite-projects/site@${version} installs and bundles cleanly for a correctly-configured consumer."
