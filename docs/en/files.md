```json
//[doc-seo]
{
    "Description": "Site's files are kept in Dignite Drive: the containers Site's fields use, how a host installs them, the Drive's permissions, Site's relative addresses, resizing and WebP on read, and the upgrade from Site's own file library."
}
```

# Files

The images and attachments of a site - what a file field holds, the share image of the SEO field and of the SEO settings, the images inserted into a CKEditor field, the favicon and fonts a theme links - are kept in **[Dignite Drive](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/index.md)**, the per-tenant drive of Dignite ABP Modules. Site references only the open edition of the Drive. The Drive stores the files, checks the uploads and decides who may upload and delete; Site serves them to the site's visitors at its own address, with the format each browser takes and any size.

Files are uploaded with the Drive's file input in the admin UI, by an AI client with the Drive's MCP tools, or from a CKEditor field. In a host with only the open Drive there is no file library to browse: a file is attached where it is used. A host that installs Drive Pro shows its library picker in place of the file input, without anything in Site changing.

## Containers

Site's fields use three Drive containers, which the host declares as **user drives** that are **read publicly**:

| Container | For | Drive preset | Accepts | Size limit |
|---|---|---|---|---|
| `images` | Pictures a content shows, share images | `Images` | JPEG, PNG, GIF, WebP | 25 MB |
| `attachments` | Documents to download | `Documents` | PDF, Office documents, text, CSV, ZIP | 20 MB |
| `theme-assets` | The binary files a [theme](rendering/scriban-templates.md) links: the favicon, web fonts | `BinaryAssets` | ICO, WOFF2, WOFF, TTF, OTF | 5 MB |

`SiteFileContainerNames` holds the three names. A file field's configuration names its container, and may name another container the host declares. The presets are the Drive's ([Setting Up the Drive](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/drive-host.md)); the `Images` preset stores a **JPEG as WebP**, turned upright, removes the metadata a design tool piles into a picture, and shrinks a picture wider than 2560 or taller than 4096 pixels. A file's type is detected from its bytes, SVG, HTML and scripts are accepted by no preset, and the same bytes uploaded twice into a container are stored once.

## Installing the Drive in the Host

Site's packages reference the Drive's contracts only; the host installs the Drive. In the host that runs Site's administration API, reference `Dignite.Drive.Application`, `Dignite.Drive.HttpApi` and `Dignite.Drive.EntityFrameworkCore` (and `Dignite.Drive.Mcp` for its MCP tools), and depend on their modules:

````csharp
[DependsOn(
    typeof(DriveApplicationModule),
    typeof(DriveHttpApiModule),
    typeof(DriveEntityFrameworkCoreModule),
    typeof(DriveMcpModule)
)]
public class MyHostModule : AbpModule
{
}
````

Call `builder.ConfigureDrive()` in the host's migrations `DbContext` and add a migration: it creates `DriveFileDescriptors`. Then declare the three containers in the `Drive` section of `appsettings.json`:

````json
{
  "Drive": {
    "Containers": {
      "images": { "Kind": "UserDrive", "PublicRead": true, "Preset": "Images" },
      "attachments": { "Kind": "UserDrive", "PublicRead": true, "Preset": "Documents" },
      "theme-assets": { "Kind": "UserDrive", "PublicRead": true, "Preset": "BinaryAssets" }
    }
  }
}
````

and choose where their bytes are stored, with any [BLOB storage provider](https://abp.io/docs/latest/framework/infrastructure/blob-storing#blob-storage-providers):

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    foreach (var containerName in SiteFileContainerNames.All)
    {
        options.Containers.Configure(containerName, container =>
        {
            container.UseDatabase();
        });
    }
});
````

Pictures are converted and resized with SkiaSharp, which the Drive and Site's public read depend on. A host that runs on Linux also references the `SkiaSharp.NativeAssets.Linux` package.

## Permissions

Files are authorized by the Drive's own permissions, not by Site's:

| Operation | Requires |
|---|---|
| Upload a file | `Drive.Files.Upload` |
| Delete a file | Being the file's uploader, or `Drive.Files.Delete` |
| Read a file | Nothing: the containers are read publicly |

Grant `Drive.Files.Upload` to the roles that edit contents and the theme, and `Drive.Files.Delete` where others' files should be deletable; see [the Drive's permissions](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/permissions.md). The tenant's `Drive.Enable` feature has to be on to upload.

## Addresses

A file's address on the site is **relative**:

````text
/api/site-public/files/{containerName}/{blobName}
````

It names no tenant: the endpoint serves the file of the tenant the request resolves to, by the host name or however the deployment resolves tenants for every other request. No host name is written into a content, so moving the public site, the API or a gateway to another host leaves every stored address valid.

The Drive returns its own address of a file (`/api/drive/public/{container}/{blob}`, naming the tenant with `?__tenant=`), and a file field or an SEO image may store it. Site serves the same file at its own address: `SiteFileUrl.Normalize` turns the one into the other, and `SiteFileUrl.Sized` and `SiteFileUrl.Absolutize`, which templates and the head metadata use, apply it first. Each consumer puts a host in front of the relative address:

| Consumer | Resolves the address against |
|---|---|
| A page of the public site (`<img src>`, `SiteFileUrl.Sized(...)` in a template) | The page's own origin |
| The `og:image` meta tag | The site's primary domain, the `Site.PrimaryDomain` setting |
| The Angular admin UI | The host of the `SiteAdmin` API, `environment.apis.SiteAdmin.url`, naming the signed-in tenant with `?__tenant=` |

An absolute address that a user typed into a field, such as the address of an external image, is left as it is. Deleting a file needs its id, which a file field keeps; a template keeps only the address.

## Serving Files

`GET` and `HEAD` on `/api/site-public/files/{containerName}/{blobName}` serve a file anonymously. The endpoint takes the file from the Drive's public read, so it serves the files of the Drive's publicly read containers; a container that is not read publicly, a deleted file and an unknown name are all answered with `404`.

* `?Width=` and `?Height=` resize an image on the way out, up to 4096 pixels a side; with both given, the image is cropped to fill the box. The source can be at most 4096 x 4096. Anything that is not an image is served as stored, and so is a GIF.
* A request whose `Accept` header lists `image/webp`, as every current browser's does for an image, gets **WebP**: a stored WebP (every JPEG uploaded to `images`) as it is, a PNG as lossless WebP, so a screenshot, a logo or a picture with text keeps every pixel and its transparency, and a JPEG stored by an earlier version as lossy WebP. Served at its stored size, a PNG or JPEG stays as stored when its WebP would be no smaller.
* A request that does not list `image/webp`, such as a social network fetching an `og:image`, gets a **JPEG** made from a stored WebP (a PNG when the WebP has transparency), and any other picture as stored.
* A GIF is never converted.
* `?Format=webp`, `?Format=jpeg` or `?Format=original` (the stored format) fixes the format whatever the request's `Accept`; without it, or with `auto`, the format is chosen as above.
* A rendered image, resized or converted, is turned upright by the photo's EXIF orientation, which the rendering does not keep.
* `/api/site-public/files/download/{containerName}/{blobName}?fileName=` serves the file as an attachment with the given file name, always as stored.
* Responses carry an `ETag`, `Cache-Control: public, max-age=86400` and `X-Content-Type-Options: nosniff`, and no `Set-Cookie`: a shared cache may refuse a response that sets one, or keep it and give its cookie to every visitor after. A request with a matching `If-None-Match` is answered with `304`. The address of a JPEG, PNG or WebP without `?Format=` also carries `Vary: Accept`.

A CDN or proxy that caches these responses must key them on `Accept` as `Vary` says; one that ignores `Vary` would hand the WebP it cached for a browser to a client that cannot read it.

Rendering is done in the server process and bounded there: a rendered image is kept in memory (64 MB, ten minutes), requests for an image that is being rendered wait for that rendering, and at most half the processor count of images render at once. `SiteImageRenderingOptions` changes these numbers and the encoding qualities. A CDN in front keeps the rendering from being repeated beyond that.

In a template, build a resized address with `SiteFileUrl.Sized` from the `Dignite.Site.Public.Templating` namespace; in Angular, use `sizedFileUrl` from `@dignite/ng.site`:

````cshtml
@using Dignite.Site.Public.Templating

@* imageUrl is a file address read from a content's field *@
<img src="@SiteFileUrl.Sized(imageUrl, 800)" alt="" />
<img src="@SiteFileUrl.Sized(imageUrl, 400, 300)" alt="" />
````

`Sized` replaces a size already on the address and keeps its other query parameters. Either size may be `null`: the other alone scales the picture in proportion, and with neither the address is returned as it is.

### Serving Files from the Public Web Application

Since addresses are relative, the public web application must answer `/api/site-public/files/` on its own origin:

* A host that runs Site in-process, with `SiteHttpApiModule`, does so already.
* A public web application that calls Site through `Dignite.Site.Public.HttpApi.Client` should also depend on `SitePublicHttpApiModule`. Its controller then serves the files through the client proxy, with the caching headers above; without it, files are still served, but with no caching headers.

A gateway in front of the administration API must also route `GET` and `HEAD` on `/api/site-public/files/` to the service that runs Site, for the previews in the admin UI, and `/api/drive/` to the service that runs the Drive, for uploads.

## Attaching Files in the Admin UI

A file field, the SEO field's share image and the default share image of the SEO settings use the Drive's file input, `drive-file-input` from `@dignite/ng.drive`, fixed to the field's container (`images` for share images). It uploads the chosen files and shows the files attached. Removing a file takes it out of the form; a file uploaded while the form is edited and removed again before it is saved is deleted from the Drive once the form is saved. A file that was in the form before is not deleted by removing it: other contents may still use it.

A host that installs Drive Pro registers its library picker under the input's replaceable component key, `Drive.FileInputComponent`, and the same forms pick from the library.

## CKEditor Image Upload

`provideSite()` of `@dignite/ng.site` registers the upload provider of `@dignite/ng.flex-fields-ckeditor`. An image inserted into a CKEditor field is uploaded to the Drive container named by the field's `CKEditor.ImagesContainerName` configuration, normally `images`, and embedded by Site's relative address. The editor shows the image from the `SiteAdmin` API's host and saves the relative address, for HTML and Markdown fields alike.

## MCP

Site has no file tools of its own. A host that adds `Dignite.Drive.Mcp` gives AI clients the Drive's tools - `drive_list_containers`, `drive_upload_file`, `drive_create_upload_url`, `drive_get_file` and `drive_delete_file` - on the same MCP server ([the Drive's MCP tools](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/drive/mcp.md)). Site's instructions tell the client which containers its fields use and what to store: a file field takes the file objects the tools return, an SEO image and a template the file's `url`. There is no tool that lists a container's files in the open Drive, so a client keeps the `id` it needs to delete a file.

## Upgrading from Site's File Library

Before 3.0, Site kept its own file library (`SiteFileDescriptors`, `SiteDirectoryDescriptors`), with these containers and addresses. Site 3.0 serves its files from the Drive, and still serves a file the Drive does not have yet from the old library, so nothing breaks while it is migrated. To migrate, in a host that installs the Drive in its own process and database (Entity Framework Core):

1. Install the Drive and declare the containers as above, with the same container names and the same storage provider and settings as before: the stored bytes are then where the Drive looks for them.
2. Reference `Dignite.Site.Drive.Migration` and depend on `SiteDriveMigrationModule`. It runs when the host seeds its data, for the host and every tenant, and in a **dry run** first: it writes nothing and logs what it would do.
3. Read the report, set `SiteDriveMigration:DryRun` to `false` and seed again. Every file keeps its id, so file fields still name it, and its address, so stored addresses keep working; a file that shared its bytes with another gets a copy of its own. The directory a file was filed in is kept on the Drive's file (`Site:DirectoryPath` in its extra properties), and the report lists every directory.
4. Grant the Drive's permissions to the roles that edit contents and the theme (see [Permissions](#permissions)).
5. Remove the migration package. The next major version of Site removes the old tables.

## See Also

* [Field Types](field-types.md)
* [SEO](seo/index.md)
* [Admin UI](admin-ui.md)
* [MCP Tools](mcp/tools.md)
