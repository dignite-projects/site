```json
//[doc-seo]
{
    "Description": "Site's files are kept in Dignite Blob Management: the containers Site's fields use, how a host installs them, who may upload and delete, how a file is uploaded, Site's relative addresses, resizing and WebP on read, the MCP file tools, and the upgrade from Site's own file library."
}
```

# Files

The images and attachments of a site - what a file field holds, the share image of the SEO field and of the SEO settings, the images inserted into a CKEditor field, the favicon and fonts a theme links - are kept in **[Dignite Blob Management](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/index.md)**, the BLOB management module of Dignite ABP Modules. Blob Management stores the files, checks the uploads against each container's rules and counts what each tenant stores. It has no rules of its own about who may do what: Site applies its own permissions before it calls, and serves the files to the site's visitors at its own address, with the format each browser takes and any size.

Files are uploaded in the admin UI with Site's file input, by an AI client with Site's file tools, or from a CKEditor field. There is no file library to browse: a file is attached where it is used.

## Containers

Site's fields use three containers, which the host declares as Blob Management containers. Site marks every file it keeps as public, so the site's visitors read them:

| Container | For | Preset | Accepts | Size limit |
|---|---|---|---|---|
| `images` | Pictures a content shows, share images | `Images` | JPEG, PNG, GIF, WebP | 25 MB |
| `attachments` | Documents to download | `Documents` | PDF, Office documents, text, CSV, ZIP | 20 MB |
| `theme-assets` | The binary files a [theme](rendering/scriban-templates.md) links: the favicon, web fonts | `BinaryAssets` | ICO, WOFF2, WOFF, TTF, OTF | 5 MB |

A container has no directories unless the host sets `AllowDirectories` on it ([Directories](#directories)). `SiteFileContainerNames` holds the three names. A file field's configuration names its container, and may name another container the host declares for the same use. The presets are Blob Management's ([Setting Up](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/setting-up.md)); the `Images` preset stores a **JPEG as WebP**, turned upright, removes the metadata a design tool piles into a picture, and shrinks a picture wider than 2560 or taller than 4096 pixels. A file's type is detected from its bytes, and SVG, HTML and scripts are accepted by no preset.

## Installing Blob Management in the Host

Site's packages reference Blob Management's contracts only; the host installs Blob Management. In the host that runs Site's administration API, reference `Dignite.Abp.BlobManagement.Application`, `Dignite.Abp.BlobManagement.HttpApi` and `Dignite.Abp.BlobManagement.EntityFrameworkCore`, and depend on their modules:

````csharp
[DependsOn(
    typeof(BlobManagementApplicationModule),
    typeof(BlobManagementHttpApiModule),
    typeof(BlobManagementEntityFrameworkCoreModule)
)]
public class MyHostModule : AbpModule
{
}
````

In the same process Blob Management is a trusted caller of Site's services: no OAuth client is registered and nothing is granted to one. Call `builder.ConfigureBlobManagement()` in the host's migrations `DbContext` and add a migration: it creates `BlobFileDescriptors` and `BlobDirectoryDescriptors`. Then declare the three containers in the `BlobManagement` section of `appsettings.json`:

````json
{
  "BlobManagement": {
    "Containers": {
      "images": { "Preset": "Images", "AllowDirectories": true },
      "attachments": { "Preset": "Documents", "AllowDirectories": true },
      "theme-assets": { "Preset": "BinaryAssets" }
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

Pictures are converted and resized with SkiaSharp, which Blob Management and Site's public read depend on. A host that runs on Linux also references the `SkiaSharp.NativeAssets.Linux` package.

Upload tickets are signed with ASP.NET Core Data Protection: a host that runs several instances persists and shares one key ring ([Hosting Modes](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/hosting.md#data-protection)). An admin UI served from another origin than the API posts its files to `/api/blob-management/uploads/`, which the API's CORS policy (`App:CorsOrigins`) has to allow.

## Permissions

Blob Management authorizes no user for files, so Site does:

| Container | Upload requires | Delete requires |
|---|---|---|
| `images` | `SiteAdmin.Contents.Create`, `SiteAdmin.Contents.Update` or `SiteAdmin.Seo.Update` | Being the file's uploader, or `SiteAdmin.Contents.Delete` |
| `attachments` | `SiteAdmin.Contents.Create` or `SiteAdmin.Contents.Update` | Being the file's uploader, or `SiteAdmin.Contents.Delete` |
| `theme-assets` | `SiteAdmin.Themes.Edit` | Being the file's uploader, or `SiteAdmin.Themes.Edit` |

A user may delete the file they uploaded only while they may still upload to its container. Reading a file needs nothing: the files are public. The tenant's `BlobManagement.Enable` feature has to be on to upload.

## Directories

A container that sets `AllowDirectories` keeps its files in a tree of directories, which Blob Management owns: Site's file picker (the `Library` input mode of a file field, below) reads and changes the tree through Blob Management's own API with the signed-in user's token, and Site adds no rule of its own to it. What a user may do is Blob Management's answer for the container (`GET api/blob-management/containers/{name}`, any signed-in user, returns it as `access` with the container's limits):

| To | The user needs, on the container |
|---|---|
| See the directory tree | `BlobManagement.Containers` - the management permission, module-wide - or `BlobManagement.Container.View` |
| Create a directory | `BlobManagement.Container.CreateDirectories` |
| Rename or move a directory | `BlobManagement.Container.UpdateDirectories` |
| Delete a directory, which has to be empty | `BlobManagement.Container.DeleteDirectories` |

The last three are grants on the container alone: not even the management permission stands in for them. Grant them to a role in ABP's resource permission dialog - the **Permissions** button of a container on Blob Management's Files page, which takes `BlobManagement.ManagePermissions` - or in a data seed with `IResourcePermissionManager`; the dev host seeds them for the `admin` role on the three containers. A user without the right to see the tree gets the files at the top of the container only, with a note, and none of the buttons that change it.

Uploading into a directory needs only the permission to upload to the container ([Permissions](#permissions)): Blob Management checks that the directory is one of the container's, and files are not hidden by their directory.

## Uploading a File

A file is uploaded in three steps, and the bytes never pass through Site's own endpoints:

1. `POST /api/site-admin/files/upload-tickets` with the container (and a `directoryId`, to file the upload in a directory) asks for a one-use ticket. Site checks the caller's permission for the container and asks Blob Management for the ticket, which carries the tenant, the container, the file's id and the uploader.
2. The client posts the file as the multipart field `file` to the ticket's `uploadUrl` - `/api/blob-management/uploads/{ticket}`, on the same host - without any credential. Blob Management checks the file against the container's limits and stores it. It is **pending**: nobody reads it.
3. `POST /api/site-admin/files/{id}/confirm` keeps the file and makes it public. Only its uploader may confirm it. A file never confirmed is deleted after a day.

Also under `api/site-admin/files`: `GET` lists the files of a container the caller may upload to (`containerName`, `filter`, and `directoryId` - none is the top of the container), `GET {id}` reads one, `GET containers` lists the containers the caller may upload to with their limits, and `DELETE {id}` deletes a file.

## Addresses

A file's address on the site is **relative**:

````text
/api/site-public/files/{containerName}/{blobName}
````

It names no tenant: the endpoint serves the file of the tenant the request resolves to, by the host name or however the deployment resolves tenants for every other request. No host name is written into a content, so moving the public site, the API or a gateway to another host leaves every stored address valid.

Blob Management returns its own address of a public file (`/blobs/{container}/{blob}`, naming the tenant with `?__tenant=`), and a file field or an SEO image may store it. Site serves the same file at its own address: `SiteFileUrl.Normalize` turns the one into the other, and `SiteFileUrl.Sized` and `SiteFileUrl.Absolutize`, which templates and the head metadata use, apply it first. Only an address whose path starts with `/blobs/` is turned. Each consumer puts a host in front of the relative address:

| Consumer | Resolves the address against |
|---|---|
| A page of the public site (`<img src>`, `SiteFileUrl.Sized(...)` in a template) | The page's own origin |
| The `og:image` meta tag | The site's primary domain, the `Site.PrimaryDomain` setting |
| The Angular admin UI | The host of the `SiteAdmin` API, `environment.apis.SiteAdmin.url`, naming the signed-in tenant with `?__tenant=` |

An absolute address that a user typed into a field, such as the address of an external image, is left as it is. Deleting a file needs its id, which a file field keeps; a template keeps only the address.

## Serving Files

`GET` and `HEAD` on `/api/site-public/files/{containerName}/{blobName}` serve a file anonymously. The endpoint takes the file from Blob Management's public read, so it serves the files marked public; a file that is not, a deleted file and an unknown name are all answered with `404`.

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

A gateway in front of the administration API must also route `GET` and `HEAD` on `/api/site-public/files/` to the service that runs Site, for the previews in the admin UI, and `/api/blob-management/` to the service that runs Blob Management, for uploads.

## Attaching Files in the Admin UI

A file field, the SEO field's share image and the default share image of the SEO settings use `site-file-input` from `@dignite/ng.site`, fixed to the field's container (`images` for share images). It checks each chosen file against the container's size and type limits, uploads it in the three steps above, and shows the files attached. Removing a file takes it out of the form; a file uploaded while the form is edited and removed again before it is saved is deleted once the form is saved. A file that was in the form before is not deleted by removing it: other contents may still use it.

A file field has a setting, **How files are attached** (`FileExplorer.InputMode`), with two values:

* **Upload directly** (`Upload`, the default): the button opens the computer's file chooser and the files chosen are uploaded at once.
* **Choose from the files already uploaded** (`Library`): the button opens a window over the container's files, a page at a time and searchable by name, in which the user picks one file - or several, when the field takes several - or uploads another into the directory shown. When the container has directories and the user may see them ([Directories](#directories)), the tree stands beside the list, and the user creates, renames and deletes directories there as far as their grants go.

The value a field stores is the same either way. A file picked from the window is only attached: the form's cleanup never deletes it.

## CKEditor Image Upload

`provideSite()` of `@dignite/ng.site` registers the upload provider of `@dignite/ng.flex-fields-ckeditor`. An image inserted into a CKEditor field is uploaded to the container named by the field's `CKEditor.ImagesContainerName` configuration, normally `images`, and embedded by Site's relative address. The editor shows the image from the `SiteAdmin` API's host and saves the relative address, for HTML and Markdown fields alike.

## MCP

Site's [MCP tools](mcp/tools.md#files) let an AI client keep files: `site_list_file_containers`, `site_create_upload_url`, `site_confirm_file`, `site_list_files`, `site_get_file` and `site_delete_file`. The bytes of an upload go from the machine the file is on to Blob Management with the command the link comes with, and never through the client. Site's instructions tell the client which containers its fields use and what to store: a file field takes the file objects the tools return, an SEO image and a template the file's `url`. The tools are authorized by the same rules as the administration API ([Permissions](#permissions)); `SiteMcpOptions.BaseUrl` sets the address a link starts with when a gateway in front does not forward the original host.

## See Also

* [Field Types](field-types.md)
* [SEO](seo/index.md)
* [Admin UI](admin-ui.md)
* [MCP Tools](mcp/tools.md)
