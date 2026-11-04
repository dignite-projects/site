```json
//[doc-seo]
{
    "Description": "Site's file library: the images and attachments content fields use, their upload rules, permissions, relative addresses, resizing and how a host serves them."
}
```

# Files

Site has its own **file library**: the images and attachments that content fields point at, the share image of the SEO field, and the images inserted into a CKEditor field. Files are uploaded from the admin UI, by an AI client over [MCP](#mcp) or from a [CKEditor field](#ckeditor-image-upload), and served to the site's visitors by an anonymous endpoint.

## Containers

The library has three [BLOB containers](https://abp.io/docs/latest/framework/infrastructure/blob-storing), each with fixed upload rules:

| Container | Accepts | Size limit | On upload |
|---|---|---|---|
| `images` | JPEG, PNG, GIF, WebP | 10 MB | A picture larger than 1920 x 1920 is shrunk to fit, keeping its aspect ratio; one that fits is stored as uploaded. Then it is re-encoded in its own format, and the result is kept only if it is smaller. |
| `attachments` | PDF, Word, Excel, PowerPoint (both the legacy and the Open XML formats), text, CSV, ZIP | 20 MB | Stored as uploaded. |
| `theme-assets` | ICO, WOFF2, WOFF, TTF, OTF | 5 MB | Stored as uploaded. The binary files a [theme](rendering/scriban-templates.md) links, such as the favicon and web fonts. Written with the theme's permission; see [Permissions](#permissions). |

`SiteFileContainerNames.Images`, `SiteFileContainerNames.Attachments` and `SiteFileContainerNames.ThemeAssets` hold the names, and `SiteFileTypes` is the table of allowed extensions and MIME types. SVG, HTML and script files are accepted by neither container: files are public, so a file that a browser renders and executes would run on the site's own origin.

A file's type is **detected from its bytes**, never taken from the uploader's claim:

* A type the container does not accept is refused with `Dignite.Abp.BlobStoring:ContentTypeNotAllowed`. A picture uploaded into `attachments` is refused the same way; it belongs in `images`.
* A file whose content contradicts the kind of its extension, such as a PDF named `report.png`, is refused with `Dignite.Abp.BlobStoring:ContentTypeMismatch`. A picture named with another picture's extension, such as a PNG named `photo.jpg`, is stored as what it is.
* A file larger than the container's limit is refused with `Dignite.Abp.BlobStoring:ContentTooLarge`, and a picture beyond 8192 x 8192 or 50 million pixels with `Dignite.Abp.BlobStoring.Imaging:ImageTooLarge`.

Uploading the same bytes into the same container twice stores them once: the second file is a new entry in the library that refers to the bytes already stored.

### Choosing the Storage Provider

Site sets the rules of the containers; the host only chooses where the bytes are stored, with any [BLOB storage provider](https://abp.io/docs/latest/framework/infrastructure/blob-storing#blob-storage-providers). ABP merges the host's configuration of a container onto Site's, so configure the provider per container:

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

Images are resized with ABP's SkiaSharp image provider, which the Site packages depend on. A host that runs on Linux also references the `SkiaSharp.NativeAssets.Linux` package.

## Permissions

The file library has no permissions of its own. Files use the permissions of the contents they belong to, and the files of `theme-assets`, which belong to the theme, those of the theme:

| Operation | Requires |
|---|---|
| Upload a file, create a directory | `SiteAdmin.Contents.Create`; in `theme-assets`, `SiteAdmin.Themes.Edit` |
| Rename or move a file | `SiteAdmin.Contents.Update`, or being the file's uploader; in `theme-assets`, `SiteAdmin.Themes.Edit` |
| Delete a file | `SiteAdmin.Contents.Delete`, or being the file's uploader; in `theme-assets`, `SiteAdmin.Themes.Edit` |
| List and manage the files of every user | `SiteAdmin.Contents`; without it, a user lists only their own files |
| Read a file | Nothing: the files of a site are public |

Directories are personal: each user files their uploads in a tree of their own, and only that user can rename, move or delete it. A directory operation needs a signed-in user; a token issued by the client credentials flow is refused.

Like the rest of the administration API, the file library is available only while the `Site.Enable` feature is on.

## Addresses

A file's address is **relative**:

````text
/api/site-public/files/{containerName}/{blobName}
````

The address names no tenant: the endpoint serves the file of the tenant the request resolves to, by the host name or however the deployment resolves tenants for every other request. An address issued by an earlier version ends in `?__tenant=`; it keeps working.

It is the address that the administration API and the MCP tools return (`FileDescriptorDto.Url`) and the value a field stores. No host name is written into a content, so moving the public site, the API or a gateway to another host leaves every stored address valid. Each consumer puts a host in front of it:

| Consumer | Resolves the address against |
|---|---|
| A page of the public site (`<img src>`, `SiteFileUrl.Sized(...)` in a template) | The page's own origin |
| The `og:image` meta tag | The site's primary domain, the `Site.PrimaryDomain` setting |
| The Angular admin UI | The host of the `SiteAdmin` API, `environment.apis.SiteAdmin.url`, naming the signed-in tenant with `?__tenant=` (a preview is a plain `<img>` request, which carries no tenant header) |

An absolute address that a user typed into a field, such as the address of an external image, is left as it is.

## Serving Files

`GET` and `HEAD` on `/api/site-public/files/{containerName}/{blobName}` serve a file anonymously. The endpoint serves only the two Site containers: another container, a deleted file and an unknown name are all answered with `404`.

* `?Width=` and `?Height=` resize an image on the way out, up to 4096 pixels a side; with both given, the image is cropped to fill the box. The source can be at most 4096 x 4096 and 16 million pixels. Anything that is not an image is served as stored, and so is a GIF.
* `/api/site-public/files/download/{containerName}/{blobName}?fileName=` serves the file as an attachment with the given file name.
* Responses carry an `ETag`, `Cache-Control: public, max-age=86400` and `X-Content-Type-Options: nosniff`, and no `Set-Cookie`: a shared cache may refuse a response that sets one, or keep it and give its cookie to every visitor after. A request with a matching `If-None-Match` is answered with `304`.

In a template, build a resized address with `SiteFileUrl.Sized` from the `Dignite.Site.Public.Templating` namespace; in Angular, use `sizedFileUrl` from `@dignite/ng.site`:

````cshtml
@using Dignite.Site.Public.Templating

@* imageUrl is a file address read from a content's field *@
<img src="@SiteFileUrl.Sized(imageUrl, 800)" alt="" />
<img src="@SiteFileUrl.Sized(imageUrl, 400, 300)" alt="" />
````

`Sized` replaces a size already on the address and keeps its other query parameters.

### Serving Files from the Public Web Application

Since addresses are relative, the public web application must answer `/api/site-public/files/` on its own origin:

* A host that runs Site in-process, with `SiteHttpApiModule`, does so already.
* A public web application that calls Site through `Dignite.Site.Public.HttpApi.Client` should also depend on `SitePublicHttpApiModule`. Its controller then serves the files through the client proxy, with the caching headers above; without it, files are still served, but with no caching headers.

A gateway in front of the administration API must also route `GET` and `HEAD` on `/api/site-public/files/` to the service that runs Site, for the previews in the admin UI.

## CKEditor Image Upload

`provideSite()` of `@dignite/ng.site` registers the upload provider of `@dignite/ng.flex-fields-ckeditor`. An image inserted into a CKEditor field is uploaded into the container named by the field's `CKEditor.ImagesContainerName` configuration, normally `images`, and embedded by its relative address. The editor shows the image from the `SiteAdmin` API's host and saves the relative address, for HTML and Markdown fields alike.

## MCP

The file tools of the MCP server (`site_list_file_containers`, `site_upload_file` and the others) reach both containers by default, each with a description that the model reads to pick one. A host can narrow the list, or change the largest upload, which defaults to 5 MB:

````csharp
Configure<SiteMcpFileOptions>(options =>
{
    options.Containers.Clear();
    options.Containers.Add(SiteFileContainerNames.Images, "Pictures for the site's pages and posts.");
    options.MaxUploadSize = 2 * 1024 * 1024;
});
````

An upload with `site_upload_file` travels base64-encoded inside the MCP request, as text the client has to write out, so it is capped at the smaller of `MaxUploadSize` and the container's own limit, and suits files of a few kilobytes. The request size limit of the MCP endpoint is raised to fit one.

### Uploading a Large File

`site_create_upload_url` is the way to upload a photo, a background or an icon: it returns a link that takes the file as the body of a `PUT`, and the command that sends it: `command` for sh, bash, zsh and cmd.exe, and `powerShellCommand` for PowerShell, where `curl` is another command. The client runs the command on the machine the file is on, so the bytes go straight to the site and never through the model.

* The link is checked when it is issued, as the upload itself would be, and again when the file arrives: the container, the file name, the directory and the largest size are fixed in it, and the upload runs as the user who asked for it, with that user's permissions.
* It is good for ten minutes and for one use, whether the upload then succeeds or not. A link that is expired, used or not the site's own is refused with `Site:080003`.
* The link is signed with ASP.NET Core Data Protection, so every instance of a deployment has to share its key ring, as it already does for cookies. The response of the `PUT` is the stored file, with its `url`.
* The link starts with the address the MCP request came in on, which is right behind a proxy whose forwarded headers the application trusts. A host behind a gateway that does not forward the original host sets `SiteMcpFileOptions.UploadBaseUrl` to the gateway's address and routes `api/site-admin/files/upload/*` to Site.
