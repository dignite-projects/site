```json
//[doc-seo]
{
    "Description": "Site's files are kept in Dignite Blob Management: the containers Site's fields use, how a host installs them, who may upload and delete, how a file is uploaded, the addresses browsers read files at, the sizes pictures are resized to, running Blob Management as a service of its own, and the MCP file tools."
}
```

# Files

The images and attachments of a site - what a file field holds, the share image of the SEO field and of the SEO settings, the images inserted into a CKEditor field, the favicon and fonts a theme links - are kept in **[Dignite Blob Management](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/index.md)**, the BLOB management module of Dignite ABP Modules. Blob Management stores the files, checks the uploads against each container's rules and counts what each tenant stores. It has no rules of its own about who may do what: Site applies its own permissions before it calls. Blob Management serves the files to the site's visitors at its own address, as they are stored or resized to one of the sizes the container allows.

Files are uploaded in the admin UI with Site's file input, by an AI client with Site's file tools, or from a CKEditor field. There is no file library to browse: a file is attached where it is used.

## Containers

Site's fields use three containers, which the host declares as Blob Management containers. Site marks every file it keeps as public, so the site's visitors read them:

| Container | For | Preset | Accepts | Size limit |
|---|---|---|---|---|
| `images` | Pictures a content shows, share images | `Images` | JPEG, PNG, GIF, WebP | 25 MB |
| `attachments` | Documents to download | `Documents` | PDF, Office documents, text, CSV, ZIP | 20 MB |
| `theme-assets` | The binary files a [theme](rendering/scriban-templates.md) links: the favicon, web fonts | `BinaryAssets` | ICO, WOFF2, WOFF, TTF, OTF | 5 MB |

A container has no directories unless the host sets `AllowDirectories` on it ([Directories](#directories)). `SiteFileContainerNames` holds the three names. A file field's configuration names one of them: Site's administration API and MCP tools reach no other container, whatever else the host declares. The presets are Blob Management's ([Setting Up](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/setting-up.md)); the `Images` preset stores a **JPEG as WebP**, and a **PNG as lossless WebP** unless it is small, 16-bit or animated, turned upright, removes the metadata a design tool piles into a picture, and shrinks a picture wider than 2560 or taller than 4096 pixels. A file's type is detected from its bytes, and SVG, HTML and scripts are accepted by no preset.

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

In the same process Site is a trusted caller of Blob Management's services: no OAuth client is registered and nothing is granted to one. Call `builder.ConfigureBlobManagement()` in the host's migrations `DbContext` and add a migration: it creates `BlobFileDescriptors` and `BlobDirectoryDescriptors`. Then declare the three containers as Blob Management containers, each with its preset, and choose where their bytes are stored, with any [BLOB storage provider](https://abp.io/docs/latest/framework/infrastructure/blob-storing#blob-storage-providers):

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure(SiteFileContainerNames.Images, container =>
    {
        container.UseBlobManagementContainer(managed => managed.AllowDirectories = true);
        container.UseImagesPreset();
    });

    options.Containers.Configure(SiteFileContainerNames.Attachments, container =>
    {
        container.UseBlobManagementContainer(managed => managed.AllowDirectories = true);
        container.UseDocumentsPreset();
    });

    options.Containers.Configure(SiteFileContainerNames.ThemeAssets, container =>
    {
        container.UseBlobManagementContainer();
        container.UseBinaryAssetsPreset();
    });

    foreach (var containerName in SiteFileContainerNames.All)
    {
        options.Containers.Configure(containerName, container =>
        {
            container.UseDatabase();
        });
    }
});
````

Blob Management declares its containers in code only; it reads no configuration section for them.

Pictures are converted and resized with SkiaSharp, which Blob Management depends on. A host that runs Blob Management on Linux also references the `SkiaSharp.NativeAssets.Linux` package. The sizes a picture may be resized to are set on the containers too ([Serving Files](#serving-files)).

The addresses a browser reaches anonymously - the public read and its resizing, and the upload with a ticket - are rate limited per client address by ASP.NET Core's limiter, which acts only where the host runs it after routing ([Rate Limits](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/hosting.md#rate-limits)):

````csharp
app.UseRouting();
app.UseRateLimiter();
````

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

A file's address is Blob Management's public address, **relative**, as the file's `url` gives it:

````text
/blobs/{containerName}/{blobName}?__tenant={tenantId}
````

A host's file has no `__tenant`. A file field, an SEO image, CKEditor's HTML and a template store the address as it is. No host name is written into a content, so moving the public site, the API or a gateway to another host leaves every stored address valid. Each consumer puts a host in front of it:

| Consumer | Resolves the address against |
|---|---|
| A page of the public site (`<img src>`, `file_url` or `SiteFileUrl.Sized(...)` in a template) | The page's own origin |
| The `og:image` meta tag and JSON-LD | The site's primary domain, the `Site.PrimaryDomain` setting |
| The Angular admin UI | The host of the `BlobManagement` API, `environment.apis.BlobManagement.url` (`apis.default` when it is not listed) |

An absolute address that a user typed into a field, such as the address of an external image, is left as it is. Deleting a file needs its id, which a file field keeps; a template keeps only the address.

Before 3.0 Site served its files at `/api/site-public/files/{containerName}/{blobName}`, and stored data may still hold such addresses. That endpoint now answers `301` with Blob Management's address of the same file, in the tenant the request resolves to, passing `?Width=` and `?Height=` on; `?Format=` is dropped, and the `download/` address redirects to the file as Blob Management serves it. `SiteFileUrl.Sized` and the admin UI recognize these addresses too, so they keep working without being rewritten.

## Serving Files

A browser reads a file straight from Blob Management, anonymously, at its address: Site is not in between. Blob Management serves the files marked public - Site marks every file it keeps public when it confirms it - and answers a file that is not, a deleted file and an unknown name with `404`. What it does on a read is described in Blob Management's documentation ([Hosting Modes](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-management/hosting.md)); for Site's pictures:

* `?width=` and `?height=` resize a JPEG, PNG or WebP on request; with both given, the picture is cropped to fill the box. Only the sizes the container allows are served, any other is answered with `400`. Anything else is served as stored, and so is a GIF.
* A picture is served in the format it is stored in, whatever the browser asks for. The `images` container stores an uploaded JPEG, and a PNG that is not small, 16-bit or animated, as WebP already; a GIF stays a GIF.
* Responses carry an `ETag` and `Cache-Control: public` for a day. A CDN in front of `/blobs/` is where resized pictures are cached: Blob Management renders a size on every request that reaches it.

The host allows on Site's containers the sizes in `SiteImageSizes.All`: Blob Management's default list (64, 128, 256, 384, 512, 768, 1024, 1536, 2048 and 4096) and the `og:image` box, 1200 and 630. In the host that runs Blob Management:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    foreach (var containerName in SiteFileContainerNames.All)
    {
        options.Containers.Configure(containerName, container =>
        {
            container.UseBlobManagementPublicSizes(SiteImageSizes.All.ToArray());
        });
    }
});
````

In a template, build a resized address with `file_url` ([Scriban Templates](rendering/scriban-templates.md)) or `SiteFileUrl.Sized` from the `Dignite.Site.Public.Templating` namespace; in Angular, use `sizedFileUrl` from `@dignite/ng.site`:

````cshtml
@using Dignite.Site.Public.Templating

@* imageUrl is a file address read from a content's field *@
<img src="@SiteFileUrl.Sized(imageUrl, 768)" alt="" />
<img src="@SiteFileUrl.Sized(imageUrl, 512, 384)" alt="" />
````

`Sized` replaces a size already on the address and keeps its other query parameters, `__tenant` among them. Either size may be `null`: the other alone scales the picture in proportion and is rounded up to the next size the containers allow, and with neither the address is returned as it is. Two sizes are written as given, since rounding them apart would change the box's shape: give a pair the containers allow. The Angular `sizedFileUrl`, meant for the admin UI's thumbnails, rounds both.

### Reaching Blob Management from Every Site

Since addresses are relative, two paths have to answer on every origin a site is served from: `/blobs/`, Blob Management's public read, and `/api/site-public/files/`, the redirect of the earlier addresses, which `Dignite.Site.Public.HttpApi` serves.

* When one application serves the site and installs both - Blob Management with `BlobManagementHttpApiModule`, and Site's HttpApi - nothing has to be routed.
* Otherwise the gateway or reverse proxy in front of each site routes `GET` and `HEAD` on `/blobs/` to the application that installs Blob Management, and on `/api/site-public/files/` to the one that runs Site's HttpApi. This is the case when the site is served by an application of its own, apart from the administration API ([When the Site Runs in Another Application](rendering/scriban-templates.md#when-the-site-runs-in-another-application)) - the site's application installs `Dignite.Site.Public.Web` and reaches Site's data remotely, so it answers neither path - and when Blob Management runs as a service of its own.
* The origin the admin UI reaches the `BlobManagement` API at answers `/blobs/`, for the previews, and `/api/blob-management/`, for uploads and the file picker's directories. When that is not the application that installs Blob Management, its gateway routes both there.

## Blob Management as a Service of Its Own

Site's application services call Blob Management through its contracts only, so a host whose Blob Management runs elsewhere swaps the three modules above for `BlobManagementHttpApiClientModule` (`Dignite.Abp.BlobManagement.HttpApi.Client`) and points the `BlobManagement` remote service at it. Site then calls it as an OAuth client with the client credentials flow, so the host also configures that client (ABP's `IdentityClients` section), and Blob Management's administrator grants it `BlobManagement.Container.Files` on the three containers. The sizes above are then declared in the service that runs Blob Management, and the Data Protection key ring that signs upload tickets is that service's alone.

In the Angular admin UI, `environment.apis.BlobManagement.url` names that service (or the gateway that routes to it): the admin UI posts uploads to it, shows files from it, and reads a container's limits and directories from it with the signed-in user's token. That token has to be one Blob Management accepts, so the admin UI asks for the scope under which the authorization server knows Blob Management, besides its own (`oAuthConfig.scope`).

## Attaching Files in the Admin UI

A file field, the SEO field's share image and the default share image of the SEO settings use `site-file-input` from `@dignite/ng.site`, fixed to the field's container (`images` for share images). It checks each chosen file against the container's size and type limits, uploads it in the three steps above, and shows the files attached. Removing a file takes it out of the form; a file uploaded while the form is edited and removed again before it is saved is deleted once the form is saved. A file that was in the form before is not deleted by removing it: other contents may still use it.

A file field has a setting, **How files are attached** (`FileExplorer.InputMode`), with two values:

* **Upload directly** (`Upload`, the default): the button opens the computer's file chooser and the files chosen are uploaded at once.
* **Choose from the files already uploaded** (`Library`): the button opens a window over the container's files, a page at a time and searchable by name, in which the user picks one file - or several, when the field takes several - or uploads another into the directory shown. When the container has directories and the user may see them ([Directories](#directories)), the tree stands beside the list, and the user creates, renames and deletes directories there as far as their grants go.

The value a field stores is the same either way. A file picked from the window is only attached: the form's cleanup never deletes it.

## CKEditor Image Upload

`provideSite()` of `@dignite/ng.site` registers the upload provider of `@dignite/ng.flex-fields-ckeditor`. An image inserted into a CKEditor field is uploaded to the container named by the field's `CKEditor.ImagesContainerName` configuration, normally `images`, and embedded by its relative address. The editor shows the image from the `BlobManagement` API's host and saves the relative address, for HTML and Markdown fields alike.

## MCP

Site's [MCP tools](mcp/tools.md#files) let an AI client keep files: `site_list_file_containers`, `site_create_upload_url`, `site_confirm_file`, `site_list_files`, `site_get_file` and `site_delete_file`. The bytes of an upload go from the machine the file is on to Blob Management with the command the link comes with, and never through the client. Site's instructions tell the client which containers its fields use and what to store: a file field takes the file objects the tools return, an SEO image and a template the file's `url`. The tools are authorized by the same rules as the administration API ([Permissions](#permissions)); `SiteMcpOptions.BaseUrl` sets the address a link starts with when a gateway in front does not forward the original host.

## See Also

* [Field Types](field-types.md)
* [SEO](seo/index.md)
* [Admin UI](admin-ui.md)
* [MCP Tools](mcp/tools.md)
