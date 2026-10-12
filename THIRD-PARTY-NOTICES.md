# Third-Party Notices

This project (Dignite.Site) is licensed under the [MIT License](LICENSE).

It consumes the following third-party component(s) via NuGet
package references. These components remain under their own license and are
**not** relicensed by this project's MIT license.

## Dignite abp-modules (flex-fields, blob-storing, aspnetcore-mcp)

- **License:** GNU Lesser General Public License v3.0 (LGPL-3.0)
- **Source:** [abp-modules repository](https://github.com/dignite-projects/abp-modules)
- **Dependency:** nuget.org packages at `10.0.0-rc.29`, referenced via `PackageReference`
- **Full license text:** see [LGPL-3.0.txt](#lgpl-30-full-text) below, or
  https://www.gnu.org/licenses/lgpl-3.0.html

### Modules used

| Module | Path |
|---|---|
| Dignite.Abp.FlexFields.Abstractions | `abp-modules/flex-fields/src/Dignite.Abp.FlexFields.Abstractions` |
| Dignite.Abp.FlexFields.Domain.Shared | `abp-modules/flex-fields/src/Dignite.Abp.FlexFields.Domain.Shared` |
| Dignite.Abp.FlexFields.Domain | `abp-modules/flex-fields/src/Dignite.Abp.FlexFields.Domain` |
| Dignite.Abp.FlexFields.EntityFrameworkCore | `abp-modules/flex-fields/src/Dignite.Abp.FlexFields.EntityFrameworkCore` |
| Dignite.Abp.FlexFields.CKEditor | `abp-modules/flex-fields/src/Dignite.Abp.FlexFields.CKEditor` |
| Dignite.Abp.FlexFields.CKEditor.Web | `abp-modules/flex-fields/src/Dignite.Abp.FlexFields.CKEditor.Web` |
| Dignite.Abp.FlexFields.Web | `abp-modules/flex-fields/src/Dignite.Abp.FlexFields.Web` |
| Dignite.Abp.BlobStoring.Pipeline | `abp-modules/blob-storing/src/Dignite.Abp.BlobStoring.Pipeline` |
| Dignite.Abp.BlobStoring.Imaging | `abp-modules/blob-storing/src/Dignite.Abp.BlobStoring.Imaging` |
| Dignite.Abp.AspNetCore.Mcp | `abp-modules/aspnetcore-mcp/src/Dignite.Abp.AspNetCore.Mcp` |

### Compliance notes

- These modules are consumed as **separate compiled assemblies** via
  `PackageReference`, not merged or statically linked into a single binary.
  Published packages are used as-is.
- Any modification to the source code of these modules must be released
  under LGPL-3.0 (in the `abp-modules` repository itself), independent of
  this project's MIT license.
- **Do not** publish this application using single-file deployment, IL
  trimming, or NativeAOT in a way that merges these assemblies into a
  non-separable binary without also satisfying LGPL-3.0 §4 (e.g. providing
  relinkable object files). Standard multi-assembly deployment (the current
  build configuration) keeps these DLLs independently replaceable, which
  satisfies the LGPL-3.0 linking requirement.
- This project's own source code (everything outside `abp-modules`) remains
  licensed under the [MIT License](LICENSE) and is not affected by the
  above.

---

## LGPL-3.0 full text

The full text of the GNU Lesser General Public License v3.0 is available at:
https://www.gnu.org/licenses/lgpl-3.0.txt

A copy is also included in the `abp-modules` repository's `LICENSE` file.
