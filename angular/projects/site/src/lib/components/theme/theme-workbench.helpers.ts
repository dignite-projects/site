import type { SiteThemeFileSummaryDto } from '../../proxy/dignite/site/admin/themes/models';
import { SiteThemeFileStatus } from '../../proxy/dignite/site/themes/site-theme-file-status.enum';

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

/** Whether the path names a static file (its last segment has an extension) rather than a Scriban template. */
export function isAssetPath(path: string): boolean {
  return extensionOf(path) !== null;
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
  if (extension !== null && !THEME_ASSET_EXTENSIONS.includes(extension)) {
    return 'Site::ThemePathExtensionNotAllowed';
  }

  return null;
}

/** The language the code editor highlights a file in, by its extension. */
export type ThemeEditorLanguage = 'html' | 'css' | 'javascript' | 'text';

export function languageOf(path: string): ThemeEditorLanguage {
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

/**
 * The address that opens a page with a route, as far as it can be written without data: its literal segments up to the
 * first parameter, when that one is optional (`{slug?}`, `{x=1}`, `{*rest}`) - `/events/{slug?}` is `/events`.
 * Null when the first parameter is required (`/news/{year}/{slug}`): that page shows one content, reached from the site.
 */
export function routeToPreviewPath(route: string): string | null {
  const kept: string[] = [];
  for (const segment of (route ?? '').split('/').filter(part => part.length > 0)) {
    if (!segment.includes('{')) {
      kept.push(segment);
      continue;
    }

    // The name runs up to a constraint (:), a default (=) or the optional mark (?); a catch-all starts with *.
    const optional = /^\{\*|^\{[^:?=}]+[?=]/.test(segment);
    if (!optional) {
      return null;
    }

    // What follows an optional parameter depends on it.
    break;
  }

  return '/' + kept.join('/');
}
