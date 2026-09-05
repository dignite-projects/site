// Two checks over a built Angular library's package.json, both answering "is this manifest complete
// enough that a consumer installing it can actually build?".
//
// 1. Emitted imports are declared.
//    ng-packagr marks every bare specifier it does not bundle as an external, but it never checks
//    that the emitted externals are actually declared. A library can therefore publish a bundle that
//    says `from '@ngx-validate/core'` while its package.json mentions no such dependency: nothing
//    fails at build time, nothing fails at `npm install`, and the consumer gets an unresolvable
//    specifier the first time they build their own app. All five libraries in this repository had at
//    least one.
//
//    The per-module `smoke-test-angular-package.mjs` cannot catch this: it seeds the throwaway
//    consumer with `...workspacePackage.dependencies`, i.e. the demo app's hand-maintained dependency
//    list, so every undeclared package is already installed before the compile it verifies. It
//    answers "does the public API still compile", which is a different and equally worthwhile
//    question.
//
// 2. The peers of everything this package drags in are satisfied.
//    Check 1 only sees the specifiers *this* package emits. It is blind to the dependencies' own
//    requirements, and those are not the installer's problem to solve here: every ABP 10.5 +
//    Angular 21 host installs with `--legacy-peer-deps` (`@abp/ng.theme.shared` pins
//    `@swimlane/ngx-datatable@~22`, whose Angular peer range stops at 20), and under that flag npm
//    does not install peerDependencies *at any depth*. A peer declared by a transitive dependency is
//    therefore simply absent unless something else in the graph happens to pull it in as a real
//    dependency, or the consuming host already has it.
//
//    So this walks the transitive `dependencies` closure - what `--legacy-peer-deps` will actually
//    install - and requires every non-optional peer anywhere in it to be either inside that closure
//    or named in this package's own `peerDependencies` (the host baseline this library declares it
//    needs). Anything else is a dangling requirement the consumer discovers at their own build.
//
//    This is exactly the shape of the 0.1.0-preview.10 failure: `@dignite/ng.flex-fields` emitted
//    `from '@abp/ng.components/tree'` and declared `@abp/ng.components` as a *peer*, which
//    `--legacy-peer-deps` never installed, and `@dignite/ng.site` named it in neither list - so the
//    published package bundled to `Could not resolve "@abp/ng.components/tree"`. Check 1 could not
//    see it (the specifier is emitted by flex-fields, not by this package, and flex-fields did
//    declare it - just in the wrong list). flex-fields `10.0.0-rc.15` fixed its half by moving
//    `@abp/ng.components` to `dependencies`; this check is what notices the next one.
//
//    Peers marked `"optional": true` in `peerDependenciesMeta` are skipped - that is the publisher
//    saying the package works without them. A package in the closure whose manifest cannot be read
//    fails the run rather than being skipped silently: an unread manifest is unchecked peers, and
//    this check is only as good as its coverage.
//
// Both checks read from the built dist directory, so they see the manifest ng-packagr actually
// publishes rather than the source one. Check 2 additionally needs an installed tree to read the
// dependencies' manifests from, and finds it by walking up from the dist directory to the nearest
// ancestor holding a `node_modules` (angular/ for this repository's `dist/site`). Run it after the
// same `yarn install` the workspace uses, i.e. anywhere the build itself could have run.
//
// Usage: node .github/scripts/check-angular-package-deps.mjs <dist-dir> [<dist-dir> ...]
//        e.g. node .github/scripts/check-angular-package-deps.mjs dist/site

import { builtinModules } from 'node:module';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';

const distDirectories = process.argv.slice(2);

if (distDirectories.length === 0) {
  throw new Error(
    'Usage: node .github/scripts/check-angular-package-deps.mjs <dist-dir> [<dist-dir> ...]',
  );
}

const NODE_BUILTINS = new Set(builtinModules);

// Module specifiers only ever appear as string literals in these four positions. Block comments are
// stripped first: ng-packagr copies JSDoc into the emitted .mjs verbatim, and an example line like
// ` * import { pinyin } from 'pinyin-pro';` is otherwise indistinguishable from a real import.
// A `/*` inside a string literal could truncate the stripped text, which can only hide a specifier
// or surface a spurious one for a human to look at — never silently pass a real miss as declared.
const SPECIFIER_PATTERNS = [
  /(?:^|[\s;}])(?:import|export)\s[^;]*?\sfrom\s*['"]([^'"]+)['"]/g,
  /(?:^|[\s;}])import\s*['"]([^'"]+)['"]/g,
  /\bimport\s*\(\s*['"]([^'"]+)['"]\s*\)/g,
  /\brequire\s*\(\s*['"]([^'"]+)['"]\s*\)/g,
];

const stripComments = source =>
  source
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/^[ \t]*\/\/.*$/gm, '');

const toPackageName = specifier =>
  specifier.startsWith('@')
    ? specifier.split('/').slice(0, 2).join('/')
    : specifier.split('/')[0];

const collectSourceFiles = directory => {
  const found = [];
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      found.push(...collectSourceFiles(path));
    } else if (/\.(mjs|js|d\.ts)$/.test(entry.name)) {
      found.push(path);
    }
  }
  return found;
};

// Check 1: every bare specifier the built bundles emit is named in this package's own manifest.
const checkEmittedImports = (root, manifest) => {
  const declared = new Set([
    ...Object.keys(manifest.dependencies ?? {}),
    ...Object.keys(manifest.peerDependencies ?? {}),
    ...Object.keys(manifest.optionalDependencies ?? {}),
  ]);

  const undeclared = new Map();

  for (const file of collectSourceFiles(root)) {
    const source = stripComments(readFileSync(file, 'utf8'));
    for (const pattern of SPECIFIER_PATTERNS) {
      pattern.lastIndex = 0;
      for (const match of source.matchAll(pattern)) {
        const specifier = match[1];
        if (specifier.startsWith('.') || specifier.startsWith('node:')) continue;

        const name = toPackageName(specifier);
        // A secondary entry point importing its own primary one is not an external dependency.
        if (name === manifest.name || NODE_BUILTINS.has(name) || declared.has(name)) continue;

        if (!undeclared.has(name)) undeclared.set(name, new Set());
        undeclared.get(name).add(file.slice(root.length + 1).replace(/\\/g, '/'));
      }
    }
  }

  if (undeclared.size === 0) {
    console.log(`✓ ${manifest.name}: every emitted specifier is declared.`);
    return true;
  }

  console.error(`✗ ${manifest.name}: imports packages it does not declare:`);
  for (const [name, files] of [...undeclared].sort(([a], [b]) => a.localeCompare(b))) {
    console.error(`    ${name}  (${[...files].sort().join(', ')})`);
  }
  console.error(
    '  Add each to "dependencies" if a consumer would not otherwise have it, or to ' +
      '"peerDependencies" if it is guaranteed by the ABP/Angular host. A new "dependencies" entry ' +
      'must also be listed in the library\'s ng-package.json "allowedNonPeerDependencies".',
  );
  return false;
};

// The installed tree the dependencies' own manifests are read from. Walking up from the dist
// directory rather than taking a path argument keeps both workflows' invocation a bare
// `<script> dist/site`, so there is one less thing for ci.yml and release.yml to drift apart on.
const findNodeModules = root => {
  for (let directory = root; ; directory = dirname(directory)) {
    const candidate = join(directory, 'node_modules');
    if (statSync(candidate, { throwIfNoEntry: false })?.isDirectory()) return candidate;
    if (dirname(directory) === directory) return null;
  }
};

const readInstalledManifest = (nodeModules, name) => {
  try {
    return JSON.parse(readFileSync(join(nodeModules, ...name.split('/'), 'package.json'), 'utf8'));
  } catch {
    return null;
  }
};

// Check 2: every non-optional peer required anywhere in the transitive `dependencies` closure is
// either in that closure or in this package's own `peerDependencies`. See the header for why
// `--legacy-peer-deps` makes this the consumer's build error rather than their install error.
const checkPeerClosure = (root, manifest) => {
  const nodeModules = findNodeModules(root);
  if (!nodeModules) {
    console.error(
      `✗ ${manifest.name}: no node_modules found at or above ${root}, so the dependencies' own ` +
        'peerDependencies cannot be read. Run the workspace install before this check.',
    );
    return false;
  }

  // Seeded from `dependencies` only: optionalDependencies are platform-specific and may legitimately
  // be absent, and peerDependencies are precisely what does NOT get installed here.
  const installed = new Set([manifest.name]);
  const queue = Object.keys(manifest.dependencies ?? {});
  const unreadable = new Set();
  const required = new Map(); // peer name -> the packages in the closure that require it

  while (queue.length > 0) {
    const name = queue.shift();
    if (installed.has(name)) continue;
    installed.add(name);

    const installedManifest = readInstalledManifest(nodeModules, name);
    if (!installedManifest) {
      unreadable.add(name);
      continue;
    }

    for (const dependency of Object.keys(installedManifest.dependencies ?? {})) {
      if (!installed.has(dependency)) queue.push(dependency);
    }

    for (const [peer, range] of Object.entries(installedManifest.peerDependencies ?? {})) {
      if (installedManifest.peerDependenciesMeta?.[peer]?.optional === true) continue;
      if (!required.has(peer)) required.set(peer, new Map());
      required.get(peer).set(name, range);
    }
  }

  if (unreadable.size > 0) {
    console.error(
      `✗ ${manifest.name}: not installed under ${nodeModules}, so their peers went unchecked: ` +
        `${[...unreadable].sort().join(', ')}. An unread manifest is unchecked peers - failing ` +
        'rather than reporting a partial pass.',
    );
    return false;
  }

  // The host baseline: what this package declares a consumer must already provide.
  const guaranteed = new Set([
    ...Object.keys(manifest.peerDependencies ?? {}),
    ...Object.keys(manifest.optionalDependencies ?? {}),
  ]);

  const dangling = [...required].filter(
    ([peer]) => !installed.has(peer) && !guaranteed.has(peer) && !NODE_BUILTINS.has(peer),
  );

  if (dangling.length === 0) {
    console.log(
      `✓ ${manifest.name}: every peer required across its ${installed.size}-package dependency ` +
        'closure is satisfied.',
    );
    return true;
  }

  console.error(
    `✗ ${manifest.name}: packages it installs require peers it does not declare, and ` +
      '--legacy-peer-deps will not install them:',
  );
  for (const [peer, requiredBy] of dangling.sort(([a], [b]) => a.localeCompare(b))) {
    console.error(`    ${peer}`);
    for (const [name, range] of [...requiredBy].sort(([a], [b]) => a.localeCompare(b))) {
      console.error(`      required by ${name} at ${range}`);
    }
  }
  console.error(
    '  Add each to this package\'s "peerDependencies" if every ABP/Angular host is guaranteed to ' +
      'have it already, or to "dependencies" (plus ng-package.json "allowedNonPeerDependencies") ' +
      'if it is not. Fixing it upstream - the requiring package moving it to its own ' +
      '"dependencies" - works too, and is what flex-fields 10.0.0-rc.15 did for @abp/ng.components.',
  );
  return false;
};

const checkPackage = distDirectory => {
  const root = resolve(distDirectory);
  if (!statSync(root, { throwIfNoEntry: false })?.isDirectory()) {
    throw new Error(`Not a built package directory: ${root}`);
  }

  const manifest = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));

  // Both run unconditionally - a failure in one says nothing about the other, and reporting both in
  // a single run beats fixing one only to rediscover the second on the next push.
  const emittedImportsClean = checkEmittedImports(root, manifest);
  const peerClosureClean = checkPeerClosure(root, manifest);
  return emittedImportsClean && peerClosureClean;
};

const allClean = distDirectories.map(checkPackage).every(Boolean);
if (!allClean) process.exitCode = 1;
