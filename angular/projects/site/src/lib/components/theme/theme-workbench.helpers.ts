import type { SiteThemeCheckDto, SiteThemeFileSummaryDto, SiteThemeIssueDto } from '../../proxy/dignite/site/admin/themes/models';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { SiteThemeFileStatus } from '../../proxy/dignite/site/themes/site-theme-file-status.enum';
import { SiteThemeRenderScope } from '../../proxy/dignite/site/admin/themes/site-theme-render-scope.enum';
import { SiteThemeIssueSeverity } from '../../proxy/dignite/site/themes/site-theme-issue-severity.enum';

/** Mirrors `SiteThemeConsts.PathPattern` (`src/Dignite.Site.Domain.Shared/Themes/SiteThemeConsts.cs`). */
export const THEME_PATH_PATTERN =
  /^(?:[A-Za-z0-9_][A-Za-z0-9_-]*\/)*[A-Za-z0-9_][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_-]+)*$/;

/** Mirrors `SiteThemeConsts.MaxPathLength`. */
export const THEME_MAX_PATH_LENGTH = 256;

/** Mirrors the default `SiteThemeConsts.AssetExtensions`; a host may allow more, which the server then accepts. */
export const THEME_ASSET_EXTENSIONS = [
  'css', 'js', 'mjs', 'json', 'map', 'txt', 'svg', 'xml', 'webmanifest', 'ics', 'csv',
];

/** The path as the server stores it: trimmed, without a leading slash. */
export function normalizeThemePath(path: string): string {
  return (path ?? '').trim().replace(/^\/+/, '');
}

/** The extension of the last segment, without the dot, or null - the server's `SiteThemePaths.GetExtension`. */
export function extensionOf(path: string): string | null {
  const name = path.slice(path.lastIndexOf('/') + 1);
  const dot = name.lastIndexOf('.');
  return dot <= 0 || dot === name.length - 1 ? null : name.slice(dot + 1).toLowerCase();
}

/**
 * Whether `name` is a language the browser knows, spelled the way it spells it (`ja`, `zh-Hans`; not `zh-hans`) -
 * the server's check for the extension of a language version, which it makes with the runtime's cultures.
 */
function isLanguageName(name: string): boolean {
  if (!/^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$/.test(name)) {
    return false;
  }

  try {
    return (
      Intl.getCanonicalLocales(name)[0] === name &&
      new Intl.DisplayNames(['en'], { type: 'language', fallback: 'none' }).of(name) !== undefined
    );
  } catch {
    return false;
  }
}

/**
 * Whether the path names the version of a template for one language - `blog/post.ja` is the Japanese version of
 * `blog/post` - which is a template, not a static file: the last segment has exactly one dot, followed by a language
 * that is not a static file's extension. Mirrors the server's `SiteThemePaths.TryGetLanguageVersion`.
 */
export function isLanguageVersionPath(path: string): boolean {
  const name = path.slice(path.lastIndexOf('/') + 1);
  const dot = name.indexOf('.');
  if (dot <= 0 || dot !== name.lastIndexOf('.') || dot === name.length - 1) {
    return false;
  }

  const extension = name.slice(dot + 1);
  return !THEME_ASSET_EXTENSIONS.includes(extension.toLowerCase()) && isLanguageName(extension);
}

/** Whether the path names a static file (its last segment has an extension) rather than a Scriban template. */
export function isAssetPath(path: string): boolean {
  return extensionOf(path) !== null && !isLanguageVersionPath(path);
}

/**
 * Why a path cannot be saved, as a localization key, or null when it can. The server decides in the end (a host may
 * allow more extensions); this only spares a round trip for the common mistakes.
 */
export function themePathProblem(path: string): string | null {
  const normalized = normalizeThemePath(path);
  if (!normalized || normalized.length > THEME_MAX_PATH_LENGTH || !THEME_PATH_PATTERN.test(normalized)) {
    return 'Site::ThemePathInvalid';
  }

  const extension = extensionOf(normalized);
  if (extension !== null && !THEME_ASSET_EXTENSIONS.includes(extension) && !isLanguageVersionPath(normalized)) {
    return 'Site::ThemePathExtensionNotAllowed';
  }

  return null;
}

/**
 * The line break a file's text is written with: `\r\n` when every line break in it is one (a file written on Windows),
 * otherwise `\n`. The code editor turns every line break into `\n` as it reads a file, and writes the file's text back
 * with this one, so that editing a line does not rewrite every line break of the file.
 */
export function lineBreakOf(text: string): '\n' | '\r\n' {
  return /\r\n/.test(text) && !/(?:^|[^\r])\n|\r(?!\n)/.test(text) ? '\r\n' : '\n';
}

/** The language the code editor highlights a file in, by its extension. */
export type ThemeEditorLanguage = 'html' | 'css' | 'javascript' | 'text';

export function languageOf(path: string): ThemeEditorLanguage {
  if (isLanguageVersionPath(path)) {
    return 'html';
  }

  switch (extensionOf(path)) {
    case null:
    case 'svg':
    case 'xml':
      return 'html';
    case 'css':
      return 'css';
    case 'js':
    case 'mjs':
    case 'json':
    case 'map':
    case 'webmanifest':
      return 'javascript';
    default:
      return 'text';
  }
}

const LAYOUT_SKELETON = `<!DOCTYPE html>
<html lang="{{ culture }}">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>{{ head?.meta_title ?? page.display_name }}</title>
    {{ site_head }}
</head>
<body>
{{ body }}
</body>
</html>
`;

const PAGE_SKELETON = `<h1>{{ page.display_name }}</h1>
{{ if content }}
    {{ for field in field_list }}
    <section>
        <h2>{{ field.display_name }}</h2>
        {{ render_field field.name }}
    </section>
    {{ end }}
{{ else }}
    {{ list = contents { max: 20 } }}
    <ul>
    {{ for item in list.items }}
        <li><a href="{{ item.url }}">{{ item.slug }}</a></li>
    {{ end }}
    </ul>
{{ end }}
`;

const PARTIAL_SKELETON = `<div>
    {{ page.display_name }}
</div>
`;

/**
 * What a new file starts with, guessed from the suggested conventions only - it is a starting point, not a type: a
 * static file starts empty, a file under `layouts/` as a layout, a name starting with `_` as a partial, anything else
 * as a page template.
 */
export function skeletonFor(path: string): string {
  const normalized = normalizeThemePath(path);
  const name = normalized.slice(normalized.lastIndexOf('/') + 1);

  if (isAssetPath(normalized)) {
    return '';
  }

  if (normalized.startsWith('layouts/')) {
    return LAYOUT_SKELETON;
  }

  return name.startsWith('_') ? PARTIAL_SKELETON : PAGE_SKELETON;
}

/** A folder or a file of the tree on the left. */
export interface ThemeTreeNode {
  /** The folder's path (`blog`, `blog/archive`) or the file's. */
  path: string;
  /** The last segment. */
  name: string;
  isFolder: boolean;
  isAsset: boolean;
  status: SiteThemeFileStatus;
  children: ThemeTreeNode[];
  /** For a folder: whether something under it differs from the live release. */
  hasChanges: boolean;
}

/**
 * The working copy's files as a tree: folders first, then files, each by name, ordinally. A folder exists only because
 * a file is in it - the theme has no empty folders.
 */
export function buildThemeTree(files: SiteThemeFileSummaryDto[]): ThemeTreeNode[] {
  const root: ThemeTreeNode = {
    path: '', name: '', isFolder: true, isAsset: false, status: SiteThemeFileStatus.Unchanged, children: [], hasChanges: false,
  };

  for (const file of files) {
    const segments = (file.path ?? '').split('/');
    let folder = root;

    segments.slice(0, -1).forEach((segment, index) => {
      const path = segments.slice(0, index + 1).join('/');
      let child = folder.children.find(node => node.isFolder && node.path === path);
      if (!child) {
        child = {
          path, name: segment, isFolder: true, isAsset: false, status: SiteThemeFileStatus.Unchanged, children: [], hasChanges: false,
        };
        folder.children.push(child);
      }

      folder = child;
    });

    folder.children.push({
      path: file.path ?? '',
      name: segments[segments.length - 1],
      isFolder: false,
      isAsset: file.isAsset,
      status: file.status,
      children: [],
      hasChanges: file.status !== SiteThemeFileStatus.Unchanged,
    });
  }

  sortAndMark(root);
  return root.children;
}

function sortAndMark(folder: ThemeTreeNode): boolean {
  folder.children.sort((a, b) =>
    a.isFolder !== b.isFolder ? (a.isFolder ? -1 : 1) : a.name < b.name ? -1 : a.name > b.name ? 1 : 0);

  let changed = false;
  for (const child of folder.children) {
    if (child.isFolder) {
      child.hasChanges = sortAndMark(child);
    }

    changed ||= child.hasChanges;
  }

  return changed;
}

/** Every folder on the way to `path`, outermost first: `blog`, `blog/archive` for `blog/archive/post`. */
export function foldersOf(path: string): string[] {
  const segments = path.split('/');
  return segments.slice(0, -1).map((_, index) => segments.slice(0, index + 1).join('/'));
}

/** The part of an HTTP error response the workbench reads: ABP's `{ error: { code, message, details } }` body. */
export interface ApiErrorResponse {
  status?: number;
  message?: string;
  error?: {
    error?: {
      code?: string;
      message?: string;
      details?: string;
      validationErrors?: { message?: string }[];
    };
  };
}

/** The message of an ABP error response, with its validation details. */
export function errorMessageOf(response: ApiErrorResponse | undefined): string {
  const error = response?.error?.error;
  const details = (error?.validationErrors ?? []).map(entry => entry.message).filter(Boolean);

  return [error?.message ?? response?.message, ...details].filter(Boolean).join('\n');
}

/** The theme path a page's `Template` renders through - the site's resolution: a trailing `.cshtml` is dropped. */
export function pageTemplatePath(template: string): string {
  const name = (template ?? '').trim();
  return normalizeThemePath(name.toLowerCase().endsWith('.cshtml') ? name.slice(0, -'.cshtml'.length) : name);
}

/** The field type of a page's SEO block: edited in its own panel and written by `site_head`, never by hand. */
const SEO_FIELD_TYPE = 'Seo';

/** A field name Scriban can read as a member (`content.fields.title`); anything else needs the indexer form. */
const SCRIBAN_MEMBER_NAME = /^[A-Za-z_][A-Za-z0-9_]*$/;

/**
 * The Scriban that prints a field of the current content. Plain values are read straight off `content.fields`; a date
 * is formatted; everything composite or rich (editor text, matrix, table, a linked content, files) goes through
 * `render_field`, which knows how to draw it. A name that is not a plain identifier (it may hold a hyphen) is read with
 * the indexer, the only form Scriban parses for it.
 */
export function snippetFor(fieldTypeName: string, name: string): string {
  const member = SCRIBAN_MEMBER_NAME.test(name) ? `content.fields.${name}` : `content.fields["${name}"]`;

  switch (fieldTypeName) {
    case 'Text':
    case 'Number':
    case 'Boolean':
    case 'Select':
    case 'Tree':
      return `{{ ${member} }}`;
    case 'DateTime':
      return `{{ ${member} | date.to_string "%Y-%m-%d" }}`;
    default:
      return `{{ render_field "${name}" }}`;
  }
}

/** One field in the sidebar: what the author sees and the snippet a click inserts. */
export interface ThemeField {
  name: string;
  displayName: string;
  fieldTypeName: string;
  snippet: string;
}

/** The fields of one content type of a page, in the order the content type arranges them. */
export interface ThemeFieldGroup {
  id: string;
  displayName: string;
  fields: ThemeField[];
}

/**
 * One group per content type of `page`, its fields in arrangement order, named as the content type names them, without
 * the SEO block; a content type left with no field is left out. Empty until both the page and the field library are
 * there.
 */
export function fieldGroupsFor(page: PageDto | undefined, library: FieldDto[] | undefined): ThemeFieldGroup[] {
  if (!page || !library) {
    return [];
  }

  const fieldsById = new Map(library.map(field => [field.id, field] as const));

  return (page.contentTypes ?? [])
    .map(contentType => ({
      id: contentType.id ?? contentType.name ?? '',
      displayName: contentType.displayName || contentType.name || '',
      fields: [...(contentType.fields ?? [])]
        .sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
        .flatMap(arranged => {
          const field = fieldsById.get(arranged.fieldId ?? '');
          if (!field?.name || field.fieldTypeName === SEO_FIELD_TYPE) {
            return [];
          }

          const fieldTypeName = field.fieldTypeName ?? '';
          return [
            {
              name: field.name,
              displayName: arranged.displayName || field.displayName || field.name,
              fieldTypeName,
              snippet: snippetFor(fieldTypeName, field.name),
            },
          ];
        }),
    }))
    .filter(group => group.fields.length > 0);
}

/**
 * The check to show after `next`: `next` itself, with the findings of earlier renders it did not redo. A check that
 * reads the theme without rendering, or renders only the pages a changed file takes part in, says nothing new about
 * the other pages - so what an earlier render found about them still stands, and still shows: an error must not go away
 * because the next save touched something else. A full render replaces every earlier finding.
 * <p>
 * A finding about pages goes when `next` rendered every page template that met it again - a partial that fails, fails
 * every page that includes it, and is fixed only when none of them fails. A finding about a run as a whole (cut short,
 * stopped, no renderer) stays until `next` brings one of its own.
 */
export function withEarlierRenderFindings(
  previous: SiteThemeCheckDto | undefined,
  next: SiteThemeCheckDto,
): SiteThemeCheckDto {
  if (!previous || next.render === SiteThemeRenderScope.All) {
    return next;
  }

  const rendered = new Set(next.renderedTemplates ?? []);
  const hasOwnRunFindings = next.issues.some(issue => issue.foundByRender && !issue.templates?.length);
  const same = (a: SiteThemeIssueDto, b: SiteThemeIssueDto) =>
    a.code === b.code && a.path === b.path && a.line === b.line && a.column === b.column && a.message === b.message;

  const earlier = previous.issues.filter(issue =>
    issue.foundByRender
    && (issue.templates?.length ? !issue.templates.every(template => rendered.has(template)) : !hasOwnRunFindings)
    && !next.issues.some(current => same(current, issue)));
  if (earlier.length === 0) {
    return next;
  }

  // Errors first, as the server sorts them; the sort is stable, so each keeps its order within a severity.
  const issues = [...next.issues, ...earlier].sort((a, b) => a.severity - b.severity);
  return { ...next, issues, hasErrors: issues.some(issue => issue.severity === SiteThemeIssueSeverity.Error) };
}
