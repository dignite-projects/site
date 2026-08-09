/**
 * Mirrors `AdminPermissions` (`src/Dignite.Site.Admin.Application.Contracts/Permissions/AdminPermissions.cs`).
 *
 * Note the group name is the bare string `Admin`, not `Site.Admin` - these are the exact strings the
 * server checks, so they cannot be prefixed here.
 */
// A plain enum, not `const enum`: templates reference these through a component property, and a
// `const enum` is erased at compile time so it has no value to read at runtime.
export enum eSitePolicyNames {
  Pages = 'Admin.Pages',
  PagesCreate = 'Admin.Pages.Create',
  PagesUpdate = 'Admin.Pages.Update',
  PagesDelete = 'Admin.Pages.Delete',

  ContentTypes = 'Admin.ContentTypes',
  ContentTypesCreate = 'Admin.ContentTypes.Create',
  ContentTypesUpdate = 'Admin.ContentTypes.Update',
  ContentTypesDelete = 'Admin.ContentTypes.Delete',

  Fields = 'Admin.Fields',
  FieldsCreate = 'Admin.Fields.Create',
  FieldsUpdate = 'Admin.Fields.Update',
  FieldsDelete = 'Admin.Fields.Delete',
  /** Separate from update on purpose: renaming a field migrates every stored value. */
  FieldsRename = 'Admin.Fields.Rename',

  FieldGroups = 'Admin.FieldGroups',
  FieldGroupsCreate = 'Admin.FieldGroups.Create',
  FieldGroupsUpdate = 'Admin.FieldGroups.Update',
  FieldGroupsDelete = 'Admin.FieldGroups.Delete',

  Contents = 'Admin.Contents',
  ContentsCreate = 'Admin.Contents.Create',
  ContentsUpdate = 'Admin.Contents.Update',
  ContentsDelete = 'Admin.Contents.Delete',
}
