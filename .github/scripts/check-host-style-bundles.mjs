// Fails when a stylesheet that a component fetches by a fixed file name is missing from a built
// application's output.
//
// Some components append `<link href="<bundleName>.css">` at runtime instead of compiling their
// third-party CSS in: ABP's `<abp-tree>` asks for `ng-zorro-antd-tree.css`, `@dignite/ng.flex-fields`'
// `Select` controls (and this repository's field arrangement picker) for `ng-zorro-antd-select.css`,
// and `@dignite/ng.flex-fields-ckeditor` for `ckeditor5.css`. The host provides each one as an
// `angular.json` `styles` entry with that `bundleName` and `inject: false`. See "Required global
// styles" in angular/projects/site/README.md.
//
// Nothing else catches a mistake in those entries:
//
//   - A missing entry does not fail the build. The only symptom is at runtime: a console error and an
//     unstyled control, or for CKEditor a field that renders as blank space.
//   - An `inject: true` entry works under `ng serve`, which does not hash file names, and breaks only
//     in a production build: `outputHashing: "all"` emits it as `<bundleName>-<hash>.css`, so the
//     fetch by the literal name 404s. For `ng-zorro-antd-tree` that meant an endless stream of 404s
//     (0.1.0-preview.13's CHANGELOG has the details), and nobody noticed it.
//
// Both mistakes show up in the build output, so this checks the output rather than angular.json. Run
// it against a production build; in a development build every entry has an unhashed name and the
// check proves nothing.
//
// Usage: node .github/scripts/check-host-style-bundles.mjs <browser-output-dir> <bundleName> [...]
//
// Pass the bundle names without `.css`. The list is not derived from anything: it has to match the
// `inject: false` entries that README section requires.

import { readdirSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';

const [outputArgument, ...bundleNames] = process.argv.slice(2);

if (!outputArgument || bundleNames.length === 0) {
  throw new Error(
    'Usage: node .github/scripts/check-host-style-bundles.mjs <browser-output-dir> <bundleName> [...]',
  );
}

const outputDirectory = resolve(outputArgument);
if (!statSync(outputDirectory, { throwIfNoEntry: false })?.isDirectory()) {
  throw new Error(`Not a directory: ${outputDirectory} (run the production build first)`);
}

const emitted = readdirSync(outputDirectory);
const escapeRegExp = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

const problems = [];
for (const bundleName of bundleNames) {
  if (emitted.includes(`${bundleName}.css`)) continue;

  // Only affects the error message, not whether the check fails. esbuild's hashes are 8 uppercase
  // alphanumerics; if that ever changes, a hashed copy falls through to the "missing" message.
  const hashed = emitted.find(file =>
    new RegExp(`^${escapeRegExp(bundleName)}-[A-Z0-9]{8}\\.css$`).test(file),
  );
  problems.push(
    hashed
      ? `${bundleName}.css - emitted as ${hashed} instead: its angular.json entry is "inject": true, ` +
          'and must be "inject": false.'
      : `${bundleName}.css - not emitted at all: angular.json has no styles entry with ` +
          `"bundleName": "${bundleName}".`,
  );
}

if (problems.length === 0) {
  console.log(
    `✓ ${bundleNames.length} fetched-by-name stylesheet(s) present under ${outputDirectory}: ` +
      bundleNames.map(name => `${name}.css`).join(', '),
  );
} else {
  console.error(`✗ Stylesheets fetched by a fixed name are missing from ${outputDirectory}:`);
  for (const problem of problems) {
    console.error(`    ${problem}`);
  }
  console.error(
    '  The components that fetch them still work but render unstyled; ng-zorro-antd-tree also ' +
      'retries forever. See "Required global styles" in angular/projects/site/README.md for the ' +
      'exact entries.',
  );
  process.exitCode = 1;
}
