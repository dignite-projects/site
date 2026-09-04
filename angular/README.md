# Dignite.Site - Angular

This workspace contains the Angular front-end for `Dignite.Site.Host`, built on the ABP Framework. For more information, visit [abp.io](https://abp.io/).

It has two parts:

* **Host** (`src/`) - the ABP application shell (login, identity, tenant/setting/feature management, and the Site feature) that talks to the `Dignite.Site.Host` backend.
* **site** (`projects/site/`) - the `@dignite/ng.site` library with the Site feature's components/services. It's consumed by Host through the `@dignite/ng.site` / `@dignite/ng.site/config` path aliases (see `tsconfig.json`), so it doesn't need to be built or published separately for local development.

## Pre-requirements

* [Node.js v18 or later](https://nodejs.org/)
* [npm](https://www.npmjs.com/) or [yarn](https://yarnpkg.com/)

## Getting Started

### Install dependencies

```bash
npm install
```

### Install ABP client-side libraries

If you haven't already, run the following command from the solution root folder:

```bash
abp install-libs
```

### Start the backend

Before running the Angular application, make sure the `Dignite.Site.Host` backend is running (default: `https://localhost:44315`).

### Start the dev server

```bash
npm start
```

Navigate to `http://localhost:4200/`. The app reloads automatically when you change source files.

## ABP CLI Commands

### Generate Proxy

Generate TypeScript proxies for the backend APIs:

```bash
abp generate-proxy -t ng
```

## Code scaffolding

Run `ng generate component component-name` to generate a new component in Host, or `ng generate component component-name --project site` for the library.

## Build

```bash
npm run build:prod   # Host application -> dist/Host
npm run build:site  # site library -> dist/site
```

## Running unit tests

```bash
npm test   # Host application
ng test site   # site library
```

## Environment Configuration

### Development

Local dev config lives in `src/environments/environment.ts` (API/OAuth issuer, redirect URIs).

### Production

The production build uses runtime environment configuration through `dynamic-env.json`, so deployment values (backend URL, OAuth issuer, redirect URIs) can be set without rebuilding the app. See [Environment](https://abp.io/docs/latest/framework/ui/angular/environment) for more information.

## Additional Resources

* [ABP Angular UI Documentation](https://abp.io/docs/latest/framework/ui/angular/overview)
* [Angular Library Development](https://angular.dev/tools/libraries)
* [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli)
