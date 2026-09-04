// Fails when one scoped package is installed more than once in a dependency tree.
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
// Deliberately install-manager-sensitive: npm dedupes the exact graph that Yarn Classic splits, so
// running this after `npm install` proves much less than running it after `yarn install`. Point it
// at a tree installed the way the workspace's own developers install.
//
// Usage: node .github/scripts/check-angular-package-duplicates.mjs <node_modules-dir> [<scope> ...]
//        e.g. node .github/scripts/check-angular-package-duplicates.mjs angular/node_modules @dignite

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';

const [nodeModulesArgument, ...scopeArguments] = process.argv.slice(2);

if (!nodeModulesArgument) {
  throw new Error(
    'Usage: node .github/scripts/check-angular-package-duplicates.mjs <node_modules-dir> [<scope> ...]',
  );
}

const scopes = (scopeArguments.length > 0 ? scopeArguments : ['@dignite']).map(scope =>
  scope.startsWith('@') ? scope : `@${scope}`,
);

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
 * packages. Only the requested scopes are recorded, but nesting is followed everywhere - a scoped
 * copy can sit under an unscoped package.
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
      if (manifest?.name && scopes.some(scope => manifest.name.startsWith(`${scope}/`))) {
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

if (installs.size === 0) {
  console.error(
    `✗ No packages matching ${scopes.join(', ')} found under ${nodeModulesRoot}. ` +
      'Either the install did not run or the scope argument is wrong - failing rather than ' +
      'reporting a vacuous pass.',
  );
  process.exitCode = 1;
} else {
  const duplicated = [...installs].filter(([, copies]) => copies.length > 1);

  if (duplicated.length === 0) {
    console.log(
      `✓ ${installs.size} package(s) matching ${scopes.join(', ')} are installed exactly once each.`,
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
