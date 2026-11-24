/**
 * The limits the administration forms check before the server does, mirroring `Dignite.Site.Domain.Shared`:
 * `IdentifierName.Pattern`, `PageConsts`, `ContentTypeConsts` and `FieldConsts`, and the FlexFields kernel's
 * `FlexFieldConsts` for a field's name and display name.
 */
export const SITE_CONSTS = {
  /** The shape of a page's, a content type's and a field's name. */
  namePattern: /^[a-z0-9][a-z0-9_-]*$/,
  page: {
    maxNameLength: 64,
    maxDisplayNameLength: 128,
    maxRouteLength: 512,
    maxTemplateLength: 256,
    routePattern: /^\S+$/,
    templatePattern: /^\/?[A-Za-z0-9_][A-Za-z0-9_/-]*$/,
  },
  contentType: {
    maxNameLength: 64,
    maxDisplayNameLength: 128,
    maxDescriptionLength: 512,
  },
  field: {
    maxNameLength: 64,
    maxDisplayNameLength: 128,
    maxGroupNameLength: 64,
  },
} as const;
