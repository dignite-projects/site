// Fails when one package is installed more than once in a dependency tree.
//
// Angular libraries register through module-scoped `new InjectionToken(...)` values, so two copies
// of the same package are two distinct DI keys, not a wasted-bytes problem. `@dignite/ng.flex-fields`
// is the concrete case: `FLEX_FIELD_TYPES` lives in that package, `provideCKEditorFieldType()`
// registers into the copy its own adapter resolved, and `FieldTypeResolver` reads the copy the host
// resolved. When those are different copies the field type is simply absent at runtime -
// `FieldTypeResolver.get('CKEditor')` throws exactly as if it had never been provided, and the
// content editor fails to render. Nothing fails at install time and nothing fails at build time,
// which is why this needs its own check rather than being caught by the build.
//
// Seen for real: flex-fields 10.0.0-rc.13's adapter packages declare their siblings at
// `^10.0.0-rc.4` while npmjs' `latest` dist-tag for those siblings still pointed at 10.0.0-rc.11.
// Yarn Classic prefers the `latest`-tagged version for any range that admits it, so it installed
// rc.11 nested under each adapter alongside the root's rc.13 - see
// https://github.com/dignite-projects/abp-modules/issues/211. The `resolutions` block in
// angular/package.json is the current workaround; this check is what notices when that block stops
// being enough, or when a future version fork reintroduces the same split for another reason.
//
// The failure mode is a property of the library, not of its name, so the targets are NOT limited to
// scopes. `ng-zorro-antd` is the unscoped case this repository actually carries: `@abp/ng.components`
// pins it at `~21.0.0-next.1` (i.e. `< 21.1.0`) while angular/projects/site/package.json asks for
// `^21.0.2` (any 21.x). The moment the root resolves to 21.1+, the installer nests a second
// ng-zorro-antd under `@abp/ng.components` - two `NZ_CONFIG` tokens and two `NzConfigService`
// instances, so a `provideNzConfig()` from one side is invisible to a component from the other.
// A scope-only check cannot see that at all, which is why targets are now "scope or bare package
// name" rather than "scope".
//
// Deliberately install-manager-sensitive: npm dedupes the exact graph that Yarn Classic splits, so
// running this after `npm install` proves much less than running it after `yarn install`. Point it
// at a tree installed the way the workspace's own developers install.
//
// Usage: node .github/scripts/check-angular-package-duplicates.mjs <node_modules-dir> [<target> ...]
//
// A target is either a scope (`@dignite` - every package under it) or an exact package name
// (`ng-zorro-antd`, or a fully-qualified `@angular/cdk`). Targets default to `@dignite` when none
// are given, which keeps the original single-scope invocation working. Note the deliberate absence
// of the "bare word means scope" coercion an earlier revision had: `dignite` used to be read as
// `@dignite`, and that guess is exactly what makes an unscoped package name unexpressible. Write
// the `@` when you mean a scope.
//
// A target that matches nothing installed fails the run rather than passing vacuously - a typo'd
// target is otherwise indistinguishable from a clean tree, and this check is only as good as its
// list.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';

const [nodeModulesArgument, ...targetArguments] = process.argv.slice(2);

if (!nodeModulesArgument) {
  throw new Error(
    'Usage: node .github/scripts/check-angular-package-duplicates.mjs <node_modules-dir> [<target> ...]',
  );
}

const targets = targetArguments.length > 0 ? targetArguments : ['@dignite'];

// `@dignite` is a scope (matches `@dignite/anything`); `ng-zorro-antd` and `@angular/cdk` are exact
// package names. The only thing that distinguishes them is the `/`: a leading `@` with no slash is
// the one shape npm reserves for a scope on its own.
const isScope = target => target.startsWith('@') && !target.includes('/');
const matchTarget = (target, packageName) =>
  isScope(target) ? packageName.startsWith(`${target}/`) : packageName === target;

const nodeModulesRoot = resolve(nodeModulesArgument);
if (!statSync(nodeModulesRoot, { throwIfNoEntry: false })?.isDirectory()) {
  throw new Error(`Not a directory: ${nodeModulesRoot} (run the install first)`);
}

const readDirectory = directory => readdirSync(directory, { withFileTypes: true });

const readManifest = packageDirectory => {
  try {
    return JSON.parse(readFileSync(join(packageDirectory, 'package.json'), 'utf8'));
  } catch {
    return null; // A directory under node_modules that is not a package (.bin, .cache, ...).
  }
};

/**
 * Every package directory reachable from one `node_modules`, including the ones nested under other
 * packages. Only the requested targets are recorded, but nesting is followed everywhere - a matching
 * copy can sit under any package, matched or not.
 */
const collectInstalls = (nodeModulesDirectory, installs) => {
  for (const entry of readDirectory(nodeModulesDirectory)) {
    if (!entry.isDirectory() || entry.name.startsWith('.')) continue;

    const packageDirectories = entry.name.startsWith('@')
      ? readDirectory(join(nodeModulesDirectory, entry.name))
          .filter(inner => inner.isDirectory())
          .map(inner => join(nodeModulesDirectory, entry.name, inner.name))
      : [join(nodeModulesDirectory, entry.name)];

    for (const packageDirectory of packageDirectories) {
      const manifest = readManifest(packageDirectory);
      if (manifest?.name && targets.some(target => matchTarget(target, manifest.name))) {
        if (!installs.has(manifest.name)) installs.set(manifest.name, []);
        installs.get(manifest.name).push({
          version: manifest.version,
          path: relative(nodeModulesRoot, packageDirectory).replace(/\\/g, '/'),
        });
      }

      const nested = join(packageDirectory, 'node_modules');
      if (statSync(nested, { throwIfNoEntry: false })?.isDirectory()) {
        collectInstalls(nested, installs);
      }
    }
  }

  return installs;
};

const installs = collectInstalls(nodeModulesRoot, new Map());

// Checked per target, not just "did anything match at all": with several targets on the command
// line, one broad scope matching would otherwise cover for a mistyped package name next to it and
// silently drop that package out of the check.
const unmatchedTargets = targets.filter(
  target => ![...installs.keys()].some(name => matchTarget(target, name)),
);

if (unmatchedTargets.length > 0) {
  console.error(
    `✗ Nothing matching ${unmatchedTargets.join(', ')} is installed under ${nodeModulesRoot}. ` +
      'Either the install did not run, or the target is wrong (a scope needs its leading "@" and ' +
      'no slash; anything else is matched as an exact package name) - failing rather than ' +
      'reporting a vacuous pass.',
  );
  process.exitCode = 1;
} else {
  const duplicated = [...installs].filter(([, copies]) => copies.length > 1);

  if (duplicated.length === 0) {
    console.log(
      `✓ ${installs.size} package(s) matching ${targets.join(', ')} are installed exactly once each.`,
    );
  } else {
    console.error('✗ These packages are installed more than once:');
    for (const [name, copies] of duplicated.sort(([a], [b]) => a.localeCompare(b))) {
      console.error(`    ${name}`);
      for (const copy of copies.sort((a, b) => a.path.localeCompare(b.path))) {
        console.error(`      ${copy.version.padEnd(16)} ${copy.path}`);
      }
    }
    console.error(
      '  Each copy carries its own module-scoped InjectionToken values, so providers registered ' +
        'against one are invisible to consumers of the other. Pin the package to a single version ' +
        'through "resolutions" in angular/package.json, or get the depending packages to agree on ' +
        'a range that resolves to one version.',
    );
    process.exitCode = 1;
  }
}
