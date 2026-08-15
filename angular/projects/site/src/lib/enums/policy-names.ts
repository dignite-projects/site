/**
 * Mirrors `SiteAdminPermissions` (`src/Dignite.Site.Admin.Application.Contracts/Permissions/SiteAdminPermissions.cs`).
 *
 * Note the group name is the bare string `SiteAdmin`, not `Site.Admin` - these are the exact strings the
 * server checks, so they cannot be prefixed further here.
 */
// A plain enum, not `const enum`: templates reference these through a component property, and a
// `const enum` is erased at compile time so it has no value to read at runtime.
export enum eSitePolicyNames {
  Pages = 'SiteAdmin.Pages',
  PagesCreate = 'SiteAdmin.Pages.Create',
  PagesUpdate = 'SiteAdmin.Pages.Update',
  PagesDelete = 'SiteAdmin.Pages.Delete',

  ContentTypes = 'SiteAdmin.ContentTypes',
  ContentTypesCreate = 'SiteAdmin.ContentTypes.Create',
  ContentTypesUpdate = 'SiteAdmin.ContentTypes.Update',
  ContentTypesDelete = 'SiteAdmin.ContentTypes.Delete',

  Fields = 'SiteAdmin.Fields',
  FieldsCreate = 'SiteAdmin.Fields.Create',
  FieldsUpdate = 'SiteAdmin.Fields.Update',
  FieldsDelete = 'SiteAdmin.Fields.Delete',
  /** Separate from update on purpose: renaming a field migrates every stored value. */
  FieldsRename = 'SiteAdmin.Fields.Rename',

  FieldGroups = 'SiteAdmin.FieldGroups',
  FieldGroupsCreate = 'SiteAdmin.FieldGroups.Create',
  FieldGroupsUpdate = 'SiteAdmin.FieldGroups.Update',
  FieldGroupsDelete = 'SiteAdmin.FieldGroups.Delete',

  Contents = 'SiteAdmin.Contents',
  ContentsCreate = 'SiteAdmin.Contents.Create',
  ContentsUpdate = 'SiteAdmin.Contents.Update',
  ContentsDelete = 'SiteAdmin.Contents.Delete',
}
