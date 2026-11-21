# Site 文件改为浏览器直连 Blob Management：改造方案（草案，2026-11-21）

> 状态：2026-11-21 已实施，结论并入 [site-files.md](site-files.md)；本文保留为决策记录。实施时与第 3、4 节不同的地方见
> 第 6 节"定案"：没有加 `SiteFileOptions` 和 `SiteMcpOptions.BlobManagementUrl`，模板检查里的尺寸警告没做。U1 已在
> abp-modules 以上传时转换的方式处理、待发布；U2 不需要（第 5 节）。

## 0. 结论

Site 的文件存在 Blob Management（下称 BM）里，但公开读还是 Site 自己的一套：`/api/site-public/files/{container}/{blob}`
由 Site 取回字节，用 SkiaSharp 重新编码，在进程内缓存后再发出。BM 拆成独立服务后，这条路要么需要 BM 补
`IBlobManagementPublicAppService` 的远程实现，要么跑不起来；即使补上，所有图片字节也要多绕一次 Site，还会被
BM 按 IP 的限流误伤。

改为：**浏览器直接读 BM 的公开地址 `/blobs/{container}/{blob}`**。Site 只保留它有自己规则要执行的那几层：
上传票据和确认、删除和列表的权限、MCP 工具、文件输入框、模板和 SEO 里拼地址的函数，外加一个旧地址跳转。
不再请 BM 补公开读的远程实现。

## 1. 参考了谁

| 项目 | 公开文件怎么给浏览器 | 图片变换在哪做 |
|---|---|---|
| BM 自己的接入设计（`blob-management/dev-docs/business-system-integration-decision.md`） | `/blobs/...` 是"页面嵌入、CDN 缓存的地址，不是 API"，网关把 `/blobs/*` 路由到 BM | BM 按容器白名单尺寸缩放；服务端缩图缓存明确不做，交给 `/blobs` 前面的 CDN |
| [Payload CMS 存储适配器](https://payloadcms.com/docs/upload/storage-adapters) | 默认经 Payload 代理以执行读权限；`disablePayloadAccessControl: true` 时地址直接指向存储或 CDN，只建议公开桶这样用 | 上传时生成 |
| [Strapi 上传 provider](https://docs.strapi.io/cms/configurations/media-library-providers/amazon-s3) | 保存 provider 返回的地址，浏览器直接读存储 | 上传时按 breakpoints 生成多个尺寸 |
| [Supabase Storage](https://supabase.com/docs/guides/storage/serving/image-transformations) | 公开桶的地址直接读 | 存储服务自己做；按 `Accept` 自动给 WebP，`format=origin` 关闭 |
| Orchard Core 媒体（BM 的 `peer-comparison.md` 有记录） | CMS 自己出 `/media/...` | CMS 自己缩放，对参数签名，磁盘缓存 |

**一致的地方**：公开文件由存储或资源服务直接给浏览器，业务系统只在需要执行读权限时才代理（Payload 的默认）；
图片变换放在资源服务或 CDN 上（Supabase、BM 自己），不在业务系统里。

**分歧**：Orchard Core 由 CMS 自己出图片，这正是 Site 现在的做法。它适合 CMS 自带存储的单体部署，不适合
"文件交给独立服务"的前提。Site 的文件既已交给 BM，就走前一种。

**没采纳**：

- 给 BM 补 `IBlobManagementPublicAppService` 的远程实现，让 Site 继续代理。代价是字节多绕一跳、限流按 Site 的
  IP 算、多一层缓存；而公开文件是匿名访问，Site 在这一步没有任何规则要执行。
- 上传时预生成多个尺寸（Strapi、Payload 的做法）。BM 已经选了"白名单尺寸，读时缩放，CDN 缓存"。

## 2. 分层：Site 留什么、去什么

判断标准：这一层有没有 Site 自己的规则要执行。

| 层 | 去留 | 理由 |
|---|---|---|
| 上传票据（`CreateUploadTicketAsync`） | 留 | 谁能往哪个容器传，是 Site 的权限（`SiteFileAuthorization`） |
| 浏览器把文件发到 BM（`POST /api/blob-management/uploads/{ticket}`） | 不经 Site | BM 签发的票据就是凭证：签了租户、容器、文件 id、上传人、大小和类型限制、过期时间，只能用一次 |
| 确认（`ConfirmAsync`） | 留 | 再查一次容器权限，并要求上传人就是当前用户，然后设为公开 |
| 删除、列表、按 id 取单个文件 | 留 | 只开放 Site 的三个容器；删除按创建人和权限判断 |
| MCP 文件工具 | 留 | 同一套规则 |
| 文件输入框、表单保存后清理 | 留 | Site 的界面 |
| 公开读（取字节、重编码、缓存） | **去** | 匿名读公开文件，没有规则要执行 |
| 模板和 SEO 拼地址（`file_url`、og:image、JSON-LD） | 留，改成生成 BM 地址 | 尺寸要落在容器白名单里，地址要放到对外出口上 |
| 旧地址 `/api/site-public/files/...` | 留，只做 301 跳转 | 已保存的内容里有大量旧地址；跳转不读字节，也不用查库 |

## 3. 设计

### 3.1 地址：原样保存 BM 给的地址

BM 对公开文件返回 `BlobManagementFileDto.Url`，形如 `/blobs/images/{32 位十六进制}.webp?__tenant={租户 id}`
（宿主文件没有 `__tenant`）。文件字段、SEO 图片、默认分享图、CKEditor 正文和模板里都**原样保存**这个地址。

- C# 的 `SiteFileUrl.Normalize` 和 Angular 的 `normalizeFileUrl` 不再把它改写成 Site 的地址，也不再去掉 `__tenant`。
- 地址保持相对，不写主机名，这沿用 Site 现有的原则（同 CmsKit）。对外出口在输出时加，见 3.2。
- 地址里带上了租户 id。站点换到别的租户时，旧内容里的地址会失效；这个场景本来就要迁移数据，可以接受。

### 3.2 对外出口

相对地址 `/blobs/...` 由谁接住：

- **单体**（BM 装在 Site 的宿主里）：宿主自己就有 `/blobs`，不用配置。
- **微服务**：每个站点域名上都要有人把 `/blobs/*` 转给 BM。可以是网关或公网站点前面的反向代理，也可以是一个
  CDN 域名。

新增一个选项 `SiteFileOptions.PublicBaseUrl`：为空时相对地址按站点自己的域名解析；配置后（如
`https://files.example.com`，或指向 BM 的 CDN），凡是 Site 输出给浏览器或爬虫的文件地址都加上这个前缀，包括
og:image、JSON-LD、`file_url`、Razor 的 `SiteFileUrl.Sized`。

BM 已有同义的 `AbpBlobStoringBlobManagementOptions.PublicBaseUrl`，但它在 BlobStoring provider 包里，只作用于
"按名字存"的那条路；`BlobManagementFileDto.Url` 不受它影响。所以向上游提 U2（第 5 节）：把这个选项挪到
Contracts 能用的位置。上游有了之后，Site 改为直接用它，删掉自己的选项。

### 3.3 尺寸

BM 只接受容器白名单里的宽高（默认 64、128、256、384、512、768、1024、1536、2048、4096），不在名单里的回 400。

- `SiteFileUrl.Sized` 对 BM 地址输出 `width=`、`height=`，并保留 `__tenant`。裁切语义不变：宽高都给就裁成这个
  框，只给一个就按比例。
- Site 自己要用的尺寸写进宿主配置：`images` 容器的白名单加上 og:image 的 1200 和 630
  （`UseBlobManagementPublicSizes`，或 BM 配置节里对应的键，键名待核）。文档写明 Site 需要哪些尺寸。
- 模板作者在 `file_url` 里写的尺寸不在名单里时怎么办，见决策 D2。

### 3.4 格式（WebP）

BM 的公开读不看 `Accept` 头。BM 的 `Images` 预设在上传时已经把 JPEG 存成 WebP，所以直连之后损失的只有两项：

- PNG 读取时不再转成无损 WebP，体积会大一些；
- 不支持 WebP 的客户端拿不到 JPEG 回退，主要是一部分抓 og:image 的社交平台爬虫。

Supabase 的做法是资源服务按 `Accept` 自动给 WebP，`format=origin` 关闭。按同样的方式向 BM 提 U1。在那之前是
接受这两项损失，还是先不切换，见决策 D1。

### 3.5 旧地址跳转

`GET|HEAD /api/site-public/files/{container}/{*blob}` 和 `.../download/{container}/{*blob}` 保留，改为 301 跳到
`BlobManagementPublicUrl.Build(container, blob, 当前租户 id, width, height)`：

- 租户沿用现在的解析方式（域名、请求头……），所以旧地址不带 `__tenant` 也能跳对。
- `Width`、`Height` 原样转成 `width`、`height`；不在白名单里的，跳过去会是 400（见 D2）。`Format` 丢弃。
- `download/` 跳过去后，文件按 BM 的规则以 inline 或 attachment 返回，不再带 Site 原来给的文件名。目前代码里
  没有人生成 `download/` 地址，影响仅限外部保存的链接。
- 不读字节、不查库，放在 Public.HttpApi，微服务下照常可用。
- 是保留跳转还是一次性改写库里存的旧地址，见决策 D3。

### 3.6 管理后台（Angular）

- `site-file-upload.service.ts:30`：发票据地址时用 `BlobManagement` 这个 API 名，不用 `SiteAdmin`。environment
  里没有 `apis.BlobManagement` 时 ABP 退回 `apis.default`，单体下不用改配置。
- 后台显示图片（`site-file-url.service.ts`、`file-preview`、CKEditor 的显示和保存两个 contributor）：识别前缀从
  `/api/site-public/files/` 改为 `/blobs/`，放到 `BlobManagement` 的 API 地址上。地址已经带 `__tenant`，不用再加。
  旧地址仍按现在的方式显示，由跳转接住。
- `normalizeFileUrl`、`sizedFileUrl`、`isSiteFileUrl` 等 `site-file-url.ts` 里的函数与 C# 的 `SiteFileUrl` 同步修改。

### 3.7 MCP

`site_create_upload_url` 现在用 `SiteMcpOptions.BaseUrl`（Site 的地址）拼票据地址；BM 在别处时这是错的。新增
`SiteMcpOptions.BlobManagementUrl`，为空时退回 `BaseUrl`（单体下正确）。`SiteMcpConsts.FileInstructions` 里关于
`url` 的说明同步修改。

### 3.8 微服务部署（宿主配置，写进文档，Site 代码不用改）

- 宿主换掉 `BlobManagementApplicationModule`、`BlobManagementHttpApiModule`、`BlobManagementEntityFrameworkCoreModule`，
  改装 `BlobManagementHttpApiClientModule`；配 `RemoteServices:BlobManagement`，以及 OAuth 客户端凭据
  （`IdentityClients`）。
- 在 BM 一侧给这个客户端授权 Site 三个容器的 `BlobManagement.Container.Files`（BM 的设计：资源权限，提供者为 `C`）。
- 网关：`/blobs/*` 和 `/api/blob-management/*` 路由到 BM。前者要能从每个站点域名到达（3.2），后者要能从后台和
  MCP 客户端到达。
- Data Protection 密钥环只属于 BM，由 BM 的所有实例共享；Site 不需要。

## 4. 改动清单

**删掉**

- Public.Application：`FilePublicAppService`、`ImageVariantRenderer`、`RenderedImageCache`、`SiteImageRenderingOptions`、
  `SiteStoredFile`、`ImageRendition`，以及对 `Dignite.Abp.BlobStoring.Imaging`、`.Imaging.SkiaSharp` 和
  `Dignite.Abp.BlobManagement.Application.Contracts` 的引用（如果只剩这里在用）。
- Public.Application.Contracts：`IFilePublicAppService`、`GetFileInput`、`SiteFileFormats`。
- Public.HttpApi.Client：`FilePublicClientProxy` 及生成清单里的对应条目。
- Domain.Shared：`SiteImageConsts`、`SiteFileTypes`（与 BM 的 `BlobManagementFileTypes` 重复），`SiteErrorCodes` 里
  有关公开读缩放的说明。
- 测试：`FilePublicAppService_Tests`。

**修改**

- Domain.Shared `SiteFileUrl`：去掉改写，改为识别 BM 地址和旧地址；`Sized` 输出 BM 的参数；`Absolutize` 接受
  文件出口前缀。
- Domain `HeadMetadataBuilder`、Public.Web 的 `SiteHeadMetadataHtml`、`SiteJsonLd`、Scriban 的 `FileUrlFunction`、
  Razor 的 `Templating.SiteFileUrl`：用新的 `SiteFileUrl` 和 `SiteFileOptions.PublicBaseUrl`。
- Public.HttpApi：`FilePublicController` 改为跳转控制器（3.5）。
- Admin.Application：`FileAdminAppService.Map` 不再 `Normalize`；`SiteFileDtos.Url` 的注释同步修改。
- Mcp：3.7。
- Angular：3.6。
- 宿主：`images` 的白名单尺寸（3.3）。
- 文档：`docs/en/files.md`（地址、读取、微服务部署）、`rendering/scriban-templates.md` 和 `razor-templates.md`
  （`file_url` 的尺寸限制）、`field-types.md`、SEO 相关页面、CHANGELOG、`dev-docs/site-files.md`。

**新增**

- `SiteFileOptions`（`PublicBaseUrl`）。
- 跳转控制器的测试；`SiteFileUrl` 和 Angular `site-file-url` 的单元测试按新规则重写。

## 5. 向上游（abp-modules）提的

- **U1 格式协商**：公开读支持按 `Accept` 自动给 WebP，响应加 `Vary: Accept`，`format=origin` 关闭（Supabase 先例）。
  不提就接受 3.4 的损失。
  **结果（2026-11-21）**：abp-modules 没有做读时协商，改为上传时转换（`97cbaaf9`，`StorePngAsWebp`：`images` 预设把
  16 KB 以上的 PNG 存成无损 WebP，同 JPEG 转 WebP 的做法）。待 abp-modules 发布。发布后 3.4 的第一项损失消失；第二项
  （不支持 WebP 的客户端拿不到 JPEG）仍在。
- **U2 对外地址前缀**：`PublicBaseUrl` 能作用于 `BlobManagementFileDto.Url` 和票据的 `UploadUrl`，或者在 Contracts
  里提供一个按配置拼出对外地址的工具。有了它 Site 就删掉自己的 `SiteFileOptions.PublicBaseUrl` 和 MCP 的新选项。
  **不需要（2026-11-21）**：见第 6 节 D4 定案。正文 HTML 存的是相对地址，站点域名上无论如何都要路由 `/blobs/*`，
  对外前缀对 Site 没有用处，不向上游提。
- 撤回上次提过的"给 `IBlobManagementPublicAppService` 补远程实现"和"按名字查文件"，都不需要了。

## 6. 待你决策

- **D1 WebP**：直连后先接受 3.4 的两项损失、同时向上游提 U1（建议）；还是等 U1 落地再切换。
- **D2 白名单外的尺寸**：`file_url` 遇到不在名单里的尺寸——向上取最近的一档（建议，只取宽或高时不改变比例；
  宽高都给时取档会改变裁切比例，这种情况报模板检查警告）；还是原样输出，由 BM 回 400；还是直接报模板错误。
- **D3 旧地址**：保留 301 跳转、下一个大版本删除（建议）；还是写数据迁移，一次性改写内容字段、SEO 设置、
  CKEditor 正文和主题模板里存的旧地址。
- **D4 对外出口**：先在 Site 加 `SiteFileOptions.PublicBaseUrl`，上游 U2 落地后改用 BM 的（建议）；还是等 U2，期间
  只支持"网关在站点域名上路由 `/blobs/*`"这一种部署。

**定案（2026-11-21）**：D1、D2、D3 按建议。D4 实施时改为不加过渡选项：CKEditor 正文和主题模板里存的是相对地址
`/blobs/...`，渲染时 Site 不改写正文 HTML，所以无论加不加前缀选项，站点域名上都必须能到达 `/blobs/*`；前缀选项
只能覆盖 og:image、JSON-LD 和 `file_url`，会让人误以为可以不配路由。于是只支持"站点域名路由 `/blobs/*` 和
`/api/blob-management/*`"这一种部署（BM 自己的设计也是这样），CDN 放在这条路由前面。`SiteFileOptions` 和
`SiteMcpOptions.BlobManagementUrl` 都不加。同样的理由下 U2 也不需要，不向上游提。

D2 的实现：Site 用 `SiteImageSizes.All`（BM 默认阶梯加上 og:image 的 630、1200）作为向上取档的依据，宿主用同一份
列表配置 `images` 容器的白名单（`UseBlobManagementPublicSizes`）。BM 独立部署时，BM 一侧要配置同样的列表，文档写明。
宽高都给、且不在名单里时原样输出（BM 回 400）；模板检查里的警告这次不做。

## 7. 没有先例、由我设计的部分

- 3.5 的旧地址跳转，以及它对 `Width`/`Height`、`download/` 的处理。
- D2 里"向上取最近一档"的规则。
- `SiteFileOptions.PublicBaseUrl` 和 `SiteMcpOptions.BlobManagementUrl` 这两个过渡选项（上游有了就删）。

## 8. 怎么验证

- 单体：现有的 EF Core、Public.Web、Mcp 测试，加上跳转控制器和 `SiteFileUrl` 的新测试；Angular 的 vitest。
- 实机：用第二个 Host 实例，确认后台上传、正文图片、og:image、JSON-LD、旧地址跳转，在单体下都正常。
- 微服务：起 BM 的独立宿主（`abp-modules/blob-management/host`），Site 宿主改装 HttpApi.Client，走一遍上传、
  确认、删除和公开读。这一步之前没做过，需要你同意改用来测试的配置。

## 9. 与其他进行中的工作

同一工作区里另一个会话在改文件字段（`FileFieldType`、新的 `FileInputMode`）、`FileAdminAppService`、Angular 的
目录树和 `site-blob-management.service.ts`，宿主的 `images`、`attachments` 也刚开了 `AllowDirectories`。本方案动到的
`FileAdminAppService.Map`、`SiteFileDtos`、Angular 的 `files/` 目录与它重叠，实现时要先等它提交，或者约定分工。
