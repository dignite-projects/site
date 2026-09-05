#!/usr/bin/env bash
# Installs the site Angular npm package in an isolated scratch project standing in for a real ABP
# Angular host app, then bundles a minimal file that imports `provideSite` from
# `@dignite/ng.site/config`. Catches the class of bug that `npm install` alone cannot: a real ES
# import inside the published bundle (e.g.
# `import { provideCKEditorFieldType } from '@dignite/ng.flex-fields-ckeditor'` in
# fesm2022/dignite-ng.site-config.mjs) pointing at a package that isn't declared as a dependency
# anywhere - install succeeds, and only a real bundler resolving that import graph fails. A
# `tsc --noEmit` type-check does not catch this either: ng-packagr's rolled-up `.d.ts` for
# `provideSite()` only exposes the opaque `EnvironmentProviders` return type, so TypeScript never
# needs to load the field types' own declaration files - only a bundler walking the actual .mjs
# import graph does. This is what caught `@dignite/ng.flex-fields-ckeditor` /
# `@dignite/ng.flex-fields-file-explorer` missing from angular/projects/site/package.json's
# `dependencies` in 0.1.0-preview.4 - see CHANGELOG.md's 0.1.0-preview.5 entry.
#
# Two modes, because the same check is worth running on two different artifacts:
#
#   packed <tarball-or-directory> [<github-token>]
#     Installs the local `npm pack` output, before anything is published. This is the release gate:
#     release.yml runs it between "Pack site Angular library" and the publish steps, so a package
#     whose import graph does not resolve never reaches a registry. It exists because it did not:
#     0.1.0-preview.10 failed this exact bundle step - `Could not resolve "@abp/ng.components/tree"`
#     from @dignite/ng.flex-fields - but the only verification at the time ran *after* publish, so
#     its "Do not publish this as the released version" message arrived to an already-published
#     package, tagged `latest`, at the first version of that package name (so `latest` could not
#     even be rolled back to a previous release). The NuGet side has always been ordered this way:
#     "Verify packed NuGet packages restore cleanly" runs before "Push to GitHub Packages".
#
#     <github-token> is optional and, when given, points every `@dignite/*` dependency the packed
#     package.json declares at its `npm:@dignite-projects/<name>@<same-range>` alias, authenticated
#     against GitHub Packages - the exact same alias angular/package.json's own `dependencies`/
#     `resolutions` blocks use, read fresh from the tarball itself rather than hardcoded, so this
#     keeps working unmodified across every future flex-fields bump. It exists because a plain
#     `npm install` here otherwise resolves those siblings straight from public npmjs, which 404s
#     for as long as a given flex-fields version is GitHub-Packages-only (workflow_dispatch-only
#     releases in abp-modules skip the tag-triggered step that mirrors to public npmjs - see
#     CHANGELOG.md's 10.0.0-rc.16 entry). Omit the token and this mode reverts to installing every
#     `@dignite/*` sibling from its plain public-npmjs name, unchanged from before this existed -
#     the right behavior again once every flex-fields dependency in play is fully public.
#
#   published <version> <github-token>
#     Installs what was actually published to GitHub Packages, after the publish step. Not
#     redundant with `packed`: the publish step rewrites the package before pushing it - renaming it
#     to `@dignite-projects/ng.site` and rewriting every `@dignite/*` dependency into the
#     `npm:@dignite-projects/...@<range>` alias form (see the long comment on that step in
#     release.yml for why). That rewrite is itself something that can be wrong, and nothing before
#     publish can exercise it, so this mode verifies the artifact a real consumer of the pre-release
#     channel actually installs, under the names it actually carries.
#
# Both modes install `--legacy-peer-deps`, matching the "Install Angular dependencies" step earlier
# in the same job (this workspace already relies on that leniency for an existing ABP/@angular
# version mismatch - see that step's own comment).
#
# The baseline below is this package's own `peerDependencies` - `@abp/ng.core`, `@abp/ng.theme.shared`,
# and the Angular/rxjs/tslib set - standing in for "a real ABP Angular host app", which always has
# them. `--legacy-peer-deps` means npm install below would not bring any of them in on its own, so
# they have to be listed here even though the package itself declares them; `@abp/ng.theme.shared`'s
# own dependencies transitively supply `@ngx-validate/core` and `@swimlane/ngx-datatable`, the
# remaining two peers.
set -euo pipefail

usage='Usage: verify-packed-npm-install.sh packed <tarball-or-directory> [<github-token>]
       verify-packed-npm-install.sh published <version> <github-token>'

mode=${1:?"$usage"}

workdir=$(mktemp -d)
trap 'rm -rf "$workdir"' EXIT

# Only `packed` mode ever populates this (see its case arm below) - `published` mode's own tarball
# already carries the alias baked in by the publish step, so it needs no override of its own.
overrides_json='{}'

case "$mode" in
  packed)
    packed_path=${2:?"$usage"}

    # Accept either the tarball itself or the directory `npm pack --pack-destination` wrote it to,
    # mirroring verify-packed-nuget-restore.sh taking the whole artifacts/ folder. More than one
    # tarball there means the pack step produced something unexpected - refuse to guess which one
    # the release is about to publish.
    if [ -d "$packed_path" ]; then
      shopt -s nullglob
      tarballs=("$packed_path"/*.tgz)
      shopt -u nullglob
      if [ ${#tarballs[@]} -ne 1 ]; then
        echo "::error::Expected exactly one *.tgz in $packed_path, found ${#tarballs[@]} - nothing to verify."
        exit 1
      fi
      tarball=${tarballs[0]}
    else
      tarball=$packed_path
    fi

    if [ ! -f "$tarball" ]; then
      echo "::error::No such packed tarball: $tarball - run the pack step first."
      exit 1
    fi

    # Copied in rather than referenced where it lies: `file:` specifiers are resolved relative to
    # the scratch package.json, and a relative path out of a mktemp dir back into the workspace
    # would depend on the runner's temp layout.
    cp "$tarball" "$workdir/package.tgz"
    site_specifier='file:./package.tgz'
    subject="the packed tarball $(basename "$tarball")"
    failure_hint='Do not publish this build.'

    github_token=${3:-}
    if [ -n "$github_token" ]; then
      cat > "$workdir/.npmrc" <<EOF
@dignite-projects:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${github_token}
EOF

      # Every @dignite/* dependency the tarball itself declares gets pointed at its
      # @dignite-projects alias, at the exact range the tarball asks for - read fresh here rather
      # than hardcoded, so a future flex-fields bump needs no matching edit in this script. `npm
      # overrides` (not a top-level dependency edit) is what's needed: @dignite/ng.site is the only
      # direct dependency of this scratch project, and the packages that actually need redirecting
      # are ITS transitive dependencies, declared inside the tarball's own package.json.
      #
      # @dignite/ng.file-explorer needs the same treatment despite never appearing in ng.site's own
      # `dependencies`: it's a transitive dependency of @dignite/ng.flex-fields-file-explorer (a
      # dependency of a dependency, one level too deep for this script to discover without resolving
      # the tree first - the same chicken-and-egg problem the override exists to route around). It
      # is, and always has been, version-locked to @dignite/ng.flex-fields in this workspace (see
      # angular/package.json's own `resolutions` block treating the pair identically), so reusing
      # ng.flex-fields' range for it is exact, not a guess.
      extract_dir=$(mktemp -d)
      tar -xzf "$tarball" -C "$extract_dir"
      overrides_json=$(node -e '
        const pkg = require(process.argv[1] + "/package/package.json");
        const overrides = {};
        for (const [name, range] of Object.entries(pkg.dependencies || {})) {
          if (name.startsWith("@dignite/")) {
            overrides[name] = `npm:@dignite-projects/${name.slice("@dignite/".length)}@${range}`;
          }
        }
        const flexFieldsRange = pkg.dependencies?.["@dignite/ng.flex-fields"];
        if (flexFieldsRange && !overrides["@dignite/ng.file-explorer"]) {
          overrides["@dignite/ng.file-explorer"] = `npm:@dignite-projects/ng.file-explorer@${flexFieldsRange}`;
        }
        console.log(JSON.stringify(overrides));
      ' "$extract_dir")
      rm -rf "$extract_dir"
    fi
    ;;

  published)
    version=${2:?"$usage"}
    github_token=${3:?"$usage"}

    cat > "$workdir/.npmrc" <<EOF
@dignite-projects:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${github_token}
EOF

    # @dignite/ng.site is aliased from the GitHub Packages name, exactly as a real consumer would
    # declare it (see angular/projects/site/README.md's "Installing" section and
    # apps/angular/package.json in Dignite.Cloud).
    site_specifier="npm:@dignite-projects/ng.site@${version}"
    subject="@dignite-projects/ng.site@${version} from GitHub Packages"
    failure_hint="The package is already on the registry - supersede it with a fixed version and do not consume ${version}."
    ;;

  *)
    echo "$usage" >&2
    exit 2
    ;;
esac

# Nothing beyond the host baseline is pre-installed here: the CKEditor and File Explorer packages
# arrive transitively, as `dependencies` of @dignite/ng.flex-fields-ckeditor and
# @dignite/ng.flex-fields-file-explorer at >= 10.0.0-rc.13. Pre-supplying them would hide the
# regression this step exists to catch - a flex-fields floor loosened back below rc.12, where those
# same packages were peers that `--legacy-peer-deps` never installs.
cat > "$workdir/package.json" <<EOF
{
  "name": "verify-npm-install-scratch",
  "private": true,
  "dependencies": {
    "@dignite/ng.site": "${site_specifier}",
    "@abp/ng.core": "~10.5.0",
    "@abp/ng.theme.shared": "~10.5.0",
    "@angular/core": "~21.2.0",
    "@angular/common": "~21.2.0",
    "@angular/forms": "~21.2.0",
    "@angular/cdk": "~21.2.0",
    "@angular/router": "~21.2.0",
    "@angular/platform-browser": "~21.2.0",
    "@angular/animations": "~21.2.0",
    "rxjs": "~7.8.0",
    "tslib": "^2.1.0"
  },
  "overrides": ${overrides_json}
}
EOF

echo "Installing ${subject} (as @dignite/ng.site) on top of the ABP/Angular host baseline..."
if ! (cd "$workdir" && npm install --no-audit --no-fund --legacy-peer-deps); then
  echo "::error::npm install of ${subject} failed - a declared dependency doesn't resolve. ${failure_hint}"
  exit 1
fi

cat > "$workdir/smoke.ts" <<'EOF'
import { provideSite } from '@dignite/ng.site/config';

export const providers = provideSite();
EOF

echo "Type-checking a minimal consumer of provideSite()..."
if ! (cd "$workdir" && npx --yes -p typescript@~5.9.0 tsc --noEmit --strict --module esnext --moduleResolution bundler --target es2022 --skipLibCheck smoke.ts); then
  echo "::error::Type-checking 'import { provideSite } from \"@dignite/ng.site/config\"' failed for ${subject}. ${failure_hint}"
  exit 1
fi

echo "Bundling the same file to prove every real import it pulls in actually resolves..."
if ! (cd "$workdir" && npx --yes esbuild smoke.ts --bundle --platform=browser --format=esm --outfile=out.js); then
  echo "::error::Bundling a minimal consumer of provideSite() failed - an import inside ${subject} (directly or transitively) doesn't resolve. ${failure_hint}"
  exit 1
fi

echo "${subject} installs and bundles cleanly for a correctly-configured consumer."
