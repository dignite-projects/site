# CLAUDE.md

Guidance for working in this repository. The product, its design and its decisions are described in
[`dev-docs/00-总体设计.md`](dev-docs/00-总体设计.md); the user-facing behavior in [`docs/en/`](docs/en/index.md).

## Documentation

- **`docs/` is the user documentation**, in the [ABP Docs module](https://abp.io/docs/latest/modules/docs)
  format that ABP's own `docs/` and abp-modules' `docs/` use: `docs/docs-langs.json`, then
  `docs/en/docs-nav.json`, `docs/en/index.md`, top-level pages for single topics and a folder per group
  (`docs/en/rendering/`, ...), each folder with its own `images/`. English is the only language so far; a
  translation is a sibling tree (`docs/ja/`) with the same paths.
- **Pages follow ABP's conventions**: a `//[doc-seo]` block first (a three-backtick `json` fence with one
  `Description`), one `#` title, title-case headings, four-backtick code fences, relative links ending in
  `.md`, and a `See Also` list last. Link to ABP's documentation with absolute `https://abp.io/docs/latest/...`
  URLs. Do not link to a heading that contains code or punctuation: its anchor depends on the renderer.
- **A page describes released behavior to its users**: integrators, template authors and people working over
  MCP. Design rationale, issue links, "since version X" history and where a class lives belong in `dev-docs/`
  or the CHANGELOG, not in `docs/`.
- **docs.dignite.com reads the tree from GitHub at each release tag**, so every file under `docs/` is public
  and versioned. A page's file name is its address: never rename a published page. A page shows up in the
  navigation only once it is in `docs-nav.json`, but it can be reached by its path either way.
- **`dev-docs/`** holds design documents and decision records for contributors. They are not published, and
  `docs/` never links to them; a design document may link to the page that documents the behavior.
- **`README.md` is packed into every NuGet package** (`common.props`), and nuget.org does not resolve links
  relative to the repository, so the README links with absolute GitHub URLs. The same goes for
  `angular/projects/site/README.md`, packed into `@dignite/ng.site`.
- **The CHANGELOG links to pages with relative paths.** When a page moves, fix the links of earlier entries;
  never rewrite an entry otherwise.
