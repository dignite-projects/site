# Site 接入开源 Drive：调研与分步方案（草案）

**状态：草案，未经确认。** 本文不是决策记录。凡标「推断」「建议」「待确认」的，都还不是决定；标「已定」的只转述
用户 2026-11-11 的原话和 abp-modules `drive/dev-docs/drive-decision.md` 的 "Decided" 一节。

调研基于：abp-modules `main` = `b19c383`（本地工作树在 `feat/drive-attaching-files`，与 `b19c383` 内容一致，已核对
`git diff` 为空）；site `main` = `2081100`；cloud `38714f7`。

---

## 0. 复述（2026-11-11 你已确认无误）

我对已定事项的理解如下，后面的设计都建立在这上面：

1. Site 只引用开源 Drive（`Dignite.Drive.Application.Contracts`、`@dignite/ng.drive`），永不依赖 Drive Pro；宿主装开源版
   还是 Pro，Site 代码一样。
2. 接入后 `SiteFileDescriptors`（连同 `SiteDirectoryDescriptors`）去掉，Site 不再有自己的文件库。
3. 开源 Drive 下读 Site 的文件不做授权、直接输出。**我的理解**：Site 的三个容器在 Drive 里都是 `PublicRead` 的
   user drive，访客匿名可读，和今天一样。
4. 9 个 `site_*` 文件工具从 Site 删掉；开源 Drive 的 5 个 `drive_*` 接替其中 5 个，另 4 个归 Drive Pro。
5. `FileFieldType` 留在 `Dignite.FlexFields.Site`；`@dignite/ng.site` 里的控件仍注册在 `FileExplorer` 键下，改为渲染
   `drive-file-input`（替换键 `Drive.FileInputComponent`），装 Pro 的宿主自动换成库选择器。
6. 权限改用 `Drive.Files.Upload` / `Drive.Files.Delete`，上传者可删自己上传的；Site 的「容器 → 业务权限」映射
   （如 `theme-assets` 要 `SiteAdmin.Themes.Edit`）去掉，由管理员给编辑角色授 Drive 权限。
7. 只装开源版的宿主里，Site 看不到全部已上传文件，但**当前显示的**文件可以删。实现方式已定：先 (b) 后 (a)，见 D3。
8. 宿主有 Drive Pro 时，迁移现有文件不能丢东西。迁移方案在 abp-modules 的决策记录里仍是 "Proposed"；本方案第 4 节的做法你已确认（D2）。
9. 已知缺口维持现状：模板里只存地址，删文件要 id。

---

## 1. Site 现在的文件子系统

### 1.1 后端

| 部分 | 位置 | 接入后 |
|---|---|---|
| 实体 `FileDescriptor`（`SiteFileDescriptors`）、`DirectoryDescriptor`（`SiteDirectoryDescriptors`）、两个 manager、仓储、`FileDescriptorSorting`、目录异常 7 个 | `Dignite.Site.Domain/Files/`，EF：`SiteDbContext*`、`EfCoreFileDescriptorRepository`；Mongo：`MongoFileDescriptorRepository`、`SiteMongoDbContext*` | 删（迁移完成后，见第 3 节第 8 步） |
| 去重：引用行 `ReferBlobName` 指向另一行的 blob，自己的 `BlobName` 只是地址；只有 owner 带 `Hash`，`(tenant, container, hash)` 活行唯一 | `FileDescriptor` / `FileDescriptorManager` | 迁移难点，见第 4 节 |
| JPEG 存成 WebP（保存前按配置决定 blob 名 `.webp`、显示名改扩展名） | `SiteImageStorageConfiguration`（Domain）+ `JpegToWebpContributor`（Admin.Application） | 挪进 blob-storing，挂在 Drive 的容器上，命名由 Drive 按存储类型定（前置 A1–A3） |
| 大图缩小时先按 EXIF 摆正（移除 ABP 的 `SkiaSharpImageResizerContributor`，换成 `UprightSkiaSharpImageResizerContributor`，全局 DI）；`ImageOrientation`（Site.Common，公开读的渲染器也用） | `SiteAdminApplicationModule`、`Dignite.Site.Common.Application/Files/ImageOrientation.cs` | 一并挪进 blob-storing（前置 A1） |
| 容器常量 `images` / `attachments` / `theme-assets`，`All` 同时是文件库的边界（admin API、公开读、MCP 只认这三个） | `SiteFileContainerNames`（Domain.Shared） | 常量留作默认名；不再是边界 |
| 类型表 `SiteFileTypes`、地址工具 `SiteFileUrl`（`Build`/`Sized`/`Absolutize`/`IsSiteFile`，相对地址、不带租户） | Domain.Shared | 留，见 1.3 |
| 容器策略（大小、类型、图片处理器、权限映射）在模块里配，宿主只选 provider | `SiteAdminApplicationModule.ConfigureFileContainers` | 删；策略改由宿主的 `Drive` 配置节 + 预设声明 |
| 权限映射：`images`/`attachments` → `SiteAdmin.Contents.Create/Update/Delete`，`theme-assets` → `SiteAdmin.Themes.Edit`；资源授权处理器 | `BlobContainerAuthorizationConfiguration*`、`FileDescriptorAuthorizationHandler`、`DirectoryDescriptorAuthorizationHandler` | 删 |
| 管理 API：`api/site-admin/files`、`.../directories`、上传链接 `PUT` 原始 body；上传体积过滤器 | `FileAdminAppService`、`DirectoryAdminAppService`、`FileUploadTicket*`、`FileAdminController`、`DirectoryAdminController`、`FileUploadSizeLimitFilter`、`DisableFormValueModelBindingAttribute`、Contracts 下 15 个 DTO/接口、C# 客户端代理 | 删 |
| 公开读：`GET/HEAD api/site-public/files/{container}/{*blob}` 与 `.../download/{container}/{*blob}`；按 `Accept` 协商 WebP（不支持 WebP 的客户端拿 JPEG）、任意宽高（≤4096）、进程内渲染缓存与并发上限、强 ETag、`Cache-Control` 一天 | `FilePublicAppService`、`ImageVariantRenderer`、`RenderedImageCache`、`SiteImageRenderingOptions`、`FilePublicController` | 改成 Drive 之上的门面，地址不变（D1，已定） |

### 1.2 公开地址 `/api/site-public/files/{container}/{blob}` 的使用方

- 存储的数据：文件字段的值（对象数组 `id/containerName/blobName/name/mimeType/size/url`，模板读 `url`）、CKEditor
  富文本里的图片地址、SEO 字段与 SEO 设置里的分享图、品牌 logo 设置（`Site.Branding.*`，cloud 的 AdministrationService
  迁移改过它们）、主题模板里手写的链接（如 favicon、字体）。
- 生成与识别：`SiteFileUrl`（Domain.Shared）；`HeadMetadataBuilder` 与 `SiteHeadMetadataHtml` 的 `og:image`（按 1200×630
  裁切）、`SiteJsonLd`、Scriban 的 `file_url`、Razor 的 `Templating.SiteFileUrl`。
- 网关：cloud 的 PublicGateway 把 `/api/site-public/**` 转给 SiteService。

### 1.3 MCP（`Dignite.Site.Mcp`）

`FileContainerTools`（`site_list_file_containers`）、`FileTools`（`site_list_files`、`site_get_file`、`site_create_upload_url`、
`site_upload_file`、`site_update_file`、`site_delete_file`）、`DirectoryTools`（`site_list_directories`、`site_create_directory`），
以及 `SiteMcpFileOptions`（容器白名单与每个容器的用途说明、内联上传上限 5 MB）、`SiteMcpFileContainerGuard`、
`SiteMcpFileServerOptionsSetup`（把 MCP 端点的 body 上限抬到能装下 5 MB 的 base64）、`McpUploadUrl`、
`McpFileNotFoundException`、`SiteMcpConsts.FileInstructions`。另有三处工具描述提到文件工具名：
`SiteSeoSettingsTools`（`site_upload_file or site_list_files`）、`SiteThemeTools`（`site_upload_file`）、`FileFieldType`
的配置键说明（`site_list_file_containers`）。测试：`test/Dignite.Site.Mcp.Tests/Files/FileTools_Tests.cs`。

### 1.4 Angular（`@dignite/ng.site`）

- `src/lib/files/`：目录树、浏览弹窗 `site-file-modal`、选择器 `site-file-picker`、上传、预览 `site-file-preview`、
  大小管道、`ObjectUrlService`、CKEditor 的三个贡献者（上传、配置、显示）、`SiteFileUrlService`、校验服务；全部经
  `public-api` 导出。
- 用到它们的地方：文件字段控件 `site-file-control`（`site-file-picker`）、SEO 字段控件和 SEO 设置页（`site-file-modal`
  + `site-file-preview`）、`provideSite()` 注册的 `CKEDITOR_UPLOAD_PROVIDER`。
- 代理：`proxy/dignite/site/admin/files/*`。
- cloud 的 Angular 没有直接用这些组件（已 grep）。

### 1.5 宿主

- `host/Dignite.Site.Host/SiteHostModule.cs`：`AbpBlobStoringFileSystemModule`，`ConfigureBlobStoring` 给三个容器逐个
  `UseFileSystem`，根目录 `App_Data/files`（文件系统 provider 按 `{租户}/{容器名}/{blob}` 分目录）。
- 种子：`SiteContentDataSeedContributor` 建了一个 `images` 文件字段。
- 迁移：`Site_RelativeFileUrls`、`Site_RenameFileContainers` 两次数据迁移都改写过存储的地址。
- cloud 的 SiteService：默认容器 `UseFileSystem`（`BlobStoring:FileSystem:BasePath`），`SiteServiceDbContext` 直接映射了
  `FileDescriptor`，引用 Site 2.2.0。

### 1.6 开源 Drive 侧的对应物（读完的结论）

- 物理 blob 名同为「32 位十六进制 + 检测类型的扩展名」，Drive 通过同名 ABP 容器读写字节。**所以同一宿主里，Drive 容器
  沿用 `images` 等名字和同一个文件系统根目录，现有字节就在 Drive 会去找的位置**，迁移不必搬字节（引用行除外，见第 4 节）。
- `FileDescriptor` 的构造函数是公开的，可指定 `id`、`blobName`、`hash`、`ownsBlob`，`CreatorId`/`CreationTime` 可写；
  `BlobName` 上限 64（Site 是 256），`Name` 上限 256（Site 是 128），`Hash`、`MimeType` 不能为空（Site 引用行的 `Hash`
  为空，个别旧行 `MimeType` 可能为空）。
- 公开读按物理 `BlobName` 找 user drive 的文件；只接受 `AllowedSizes` 阶梯（64…4096）里的宽高；不协商格式；无渲染缓存；
  地址带 `?__tenant=`。
- `drive-file-input`：输入 `containerName`、`multiple`、`disabled`，值 `DriveFileDto[]`；只有上传和「从值里移除」，没有
  缩略图、没有删除。
- Drive 依赖的 blob-storing、aspnetcore-mcp 自 `v10.7.6` 起代码未改（`git diff v10.7.6 b19c383` 只动了
  `Directory.Packages.props` 和一份 dev-doc）。

---

## 2. 有没有在用的用户和数据

| 来源 | 结果 |
|---|---|
| 时机智算官网的 Site MCP（`site_list_files`，只读） | `images`、`attachments`、`theme-assets` 都是 0 个文件。注意该工具在调用者没有 Contents 管理权限时只列本人上传的。 |
| 另一个已连接的 Site MCP 端点（不知道对应哪个部署） | `images` 有 **311** 个文件，最新的是 2026-11-05 上传的 `campus-notice-form.jpg`。 |
| 第三个 Site MCP 端点、`site-local` | 一个超时，一个连接失败，没查到。 |
| cloud 源码 | 生产部署（`etc/helm`、`etc/deploy`）用 Site 2.2.0；开发库在 2026-10-12 时有 138 行文件描述符。 |

结论：**生产上有真实数据**。

你 2026-11-11 的补充（D7）：
- cloud 的 Site 里有文件，但只有宿主（host，`TenantId` 为空）有，各租户都没有（你确认「租主就是宿主」）。
- cloud 里 Drive 装在 SiteService 进程内。
- `D:\dignite-studio\Yuantong` 项目也有文件。我查了它的代码：Lyqgm 单体，引用 Site 2.3.0，EF Core + SQL Server，
  关了多租户；`images`、`attachments` 两个容器都 `UseDatabase()`，字节在 ABP 的 `AbpBlobs` 表里；宿主代码里直接
  用了 `SiteFileContainerNames.All`（`src/Lyqgm.Domain/LyqgmDomainModule.cs:74`）。

所以已知的两个部署都是「Drive 与 Site 同进程同库」，迁移不需要搬字节，也不需要 G4。仍不清楚的只有引用行有多少、
有没有超长的旧 blob 名，迁移器的 dry-run 报告会给出来。

---

## 3. 分步方案

每步单独构建、测试、本地提交，不推送。构建与测试统一由我在主工作区跑，日志落到 scratchpad，只看错误行。
site 的 `CLAUDE.md` 只规定文档；构建、迁移沿用仓库现有做法（宿主 `Migrations/` 用 `dotnet ef`，实际验证按「第二个 Host
实例跑在 :44399、用库副本」的办法）。

### 前置：abp-modules 的改动（JPEG 存成 WebP、先摆正再缩放）

**单独开分支、单独提 PR，等你决定；本方案不去改 abp-modules。** Site 第 1 步不依赖它，第 2 步的打包方式随它变（见第 2 步）；第 3 步要它才能保住
「JPEG 存成 WebP」和「手机照片缩小后不横过来」。

来源：2026-11-11 你的思路——`JpegToWebpContributor` 放进 `abp-modules/blob-storing/src`；转码要么在 Site 这边的容器上
跑，再把改名后的数据一起交给 Drive，要么配在 Drive 的容器上、blob 名由 Site 传过去；Drive 的上传不对外开放。
按代码落地后与原话的不同，逐条在本节末尾列出。

**依据（代码）**：Site 的文件接入后走的是 Drive 的**文件 API**（user drive），不是 provider（system 容器）：浏览器、
CKEditor、MCP 都直接把文件传给 Drive，由 Drive 用 `Drive.Files.Upload` 授权（`DriveFileAppService.AuthorizeUploadAsync`）。
所以只有挂在 **Drive 容器**上的处理器才会被每条上传路径经过（装了 Drive Pro 之后它的上传也一样）。

- **A1. blob-storing：新包 `Dignite.Abp.BlobStoring.Imaging.SkiaSharp`。**
  放 `JpegToWebpContributor`（及 `StoreJpegAsWebp(quality)`）、`ImageOrientation`、`UprightSkiaSharpImageResizerContributor`。
  模块依赖 `AbpImagingSkiaSharpModule`，并做 Site 现在做的两件事：移除 ABP 的 `SkiaSharpImageResizerContributor`
  （由「先摆正再缩放」的那个代替），把 `SkiaSharpResizerOptions` 默认设为 Linear + Mipmap、质量 85（宿主的 `Configure`
  在后面执行，仍可覆盖）。不放进现有的 `Dignite.Abp.BlobStoring.Imaging`：它只依赖 `Volo.Abp.Imaging.Abstractions`，
  与图像库无关，这三个类直接调 SkiaSharp。
- **A2. Pipeline：通用的「存储类型」查询。** 容器配置上存一张类型映射，`StoreJpegAsWebp` 往里写 `image/jpeg → image/webp`，
  `Dignite.Abp.BlobStoring.Pipeline` 提供 `BlobContainerConfiguration.GetStoredContentType(mimeType)`。Drive 因此不需要认识
  WebP；以后别的格式转换处理器用同一个办法声明。（即 Site 的 `SiteImageStorageConfiguration.GetStoredMimeType`，通用化。）
- **A3. Drive：保存前按存储类型起名。** `FileDescriptorManager.StoreOrShareAsync` 检测出类型后先查
  `GetStoredContentType`，用它生成物理 blob 名（`.webp`）。显示名：user drive 里改成存储类型的扩展名（下载名正确；去重命中时
  同样改）；system 容器的名字是应用的地址，**不改**，只有物理名带 `.webp`。去重仍用上传字节的哈希。Images 预设加参数
  `StoreJpegAsWebp`（默认开，宿主可在 `PresetOptions` 里关）和 `WebpQuality`（默认 85）；预设顺序与 Site 现在一致：大小上限 → 类型 →
  去元数据 → 转 WebP（同一次解码里摆正并缩进框）→ 缩放。
  这要 `Dignite.Drive.Domain` 引用 A1 的包，SkiaSharp 就成了每个 Drive 宿主的硬依赖（Linux 上还要
  `SkiaSharp.NativeAssets.Linux`）。另一种是不进预设，由宿主代码用 `DrivePresetOptions` 注册一个自定义预设，Drive 不碰
  SkiaSharp，但纯配置的 Drive 服务就开不了 WebP。**你 2026-11-11 选了前者，且默认开**（D8）。

**与你原话不同的地方**：

1. **不在 Site 这边跑流水线。** 让 Site 的文件先经过 Site，要么 Site 做一个转手的上传接口——决策记录 "Who may upload and
   delete" 已否掉，Drive Pro 会因此替换不了上传；要么改走 provider 入口——文件进了 system 容器，Pro 的文件库看不到，Site
   又得有自己的文件表。即便做成，CKEditor 和 MCP 的上传直接到 Drive，只有一部分 JPEG 会被转码。
2. **blob 名不由 Site 传。** user drive 的物理名由 Drive 生成（决策记录已定），文件 API 也只收显示名；真正要的是「名字在
   保存前就和存储类型一致」，Drive 用 A2 自己就能做到。
3. **新包，而不是放进 `Dignite.Abp.BlobStoring.Imaging`**（原因见 A1）。
4. **「存储类型」做成 Pipeline 的通用查询**，而不是让 Drive 认识 WebP。
5. **摆正的缩放器一起挪**，顺带解决 G2。
6. 「Drive 的上传不开放」：对匿名用户成立（BLOB API 只认白名单客户端、拒绝带用户的 token）；但持有 `Drive.Files.Upload`
   的用户可以不经 Site 直接调 Drive 的文件 API，这是设计如此，也正是转码必须放在 Drive 容器上的原因。

### 第 1 步：abp-modules 依赖对齐到 10.7.6

- 改：`common.props` 的 `DigniteAbpModulesVersion` 10.7.5 → 10.7.6（已发布）；npm 的 `@dignite/ng.flex-fields*` 不动。
- 验证：`dotnet build`、`dotnet test` 全绿。
- 理由：与接入无关的小升级，单独一步好回滚；第 2 步若用整仓预发布版本，集成分支会再改这个值。

### 第 2 步：引用未发布的 Drive

**建议做法**：

1. **前置改动未合并时**（大概率）：在带前置改动的 abp-modules 分支上，整仓打包成一个**本地预发布版本**：
   `dotnet pack Dignite.Abp.Modules.slnx -c Release -p:PackDrive=true -p:Version=10.7.7-local.N -o D:\dignite-projects\_local-feed`（目录在所有仓库
   之外，每次重新打包 N 加一）。整仓而不只打 Drive，是因为 A2 改了已发布的 `Dignite.Abp.BlobStoring.Pipeline`：同一个
   10.7.6 在本地和 nuget.org 上内容不同，是最难查的那类问题。整仓同一个预发布版本，Site 集成分支的
   `DigniteAbpModulesVersion` 设成它，所有 `Dignite.Abp.*` / `Dignite.Drive.*` 版本一致，不会 NU1605；N 递增也省掉清全局缓存。
   **前置改动不做或已发布时**：只打 Drive，版本按 lockstep 用 `10.7.6`（依赖的 blob-storing、aspnetcore-mcp 从 nuget.org
   解析，代码与打包时一致，见 1.6）；重新打包要先删全局缓存里的 `dignite.drive.*/10.7.6`。
2. 集成分支上改仓库的 `NuGet.Config`：加文件夹源 `dignite-local`（`../_local-feed`），`packageSourceMapping` 里把 `Dignite.*`
   同时映射到它和 nuget.org。分支推送前（Drive 发布后）撤掉这个改动。
   （原先打算改本机用户级配置，实施时发现那个文件里存着凭据，就不去碰它，改在分支上做。另外，用户级配置一旦加了
   mapping，会影响本机所有仓库的还原。）
3. Angular：在 abp-modules 构建 `@dignite/ng.drive`，`npm pack` 成 tgz 放进同一目录；集成分支上用
   `"@dignite/ng.drive": "file:<tgz>"`。重新打包后清 yarn 缓存。

**取舍**：

- 好处：Site 的 csproj / package.json 写法和将来发布后一样（`PackageReference` + 版本号），到时只改版本号和 npm 依赖源。
  不用发布任何东西。
- 代价：集成分支在 CI 上 restore 不了，**只能本地验证，Drive 发布前不能推送或合并**；`file:` 依赖写进了
  `package.json` / `yarn.lock`，发布后要换回 registry 版本（单独一个提交）。
- 不选 `ProjectReference` 指向兄弟仓库：2026-10-12 刚从这种耦合里退出来。
- 不选 GitHub Packages 预发布：等于发布；记忆里也记过「只在私有源」这条路的代价（每个贡献者配 token、fork PR 的 CI 挂掉）。
- `@dignite/ng.site` 对 `@dignite/ng.drive` 用 `dependencies` 还是 `peerDependencies`，按项目里「只有消费者一定已经有的
  才放 peer」的判据，用 `dependencies`（D6，已定）；装 Pro 的宿主会自己也依赖它，版本范围兼容时 yarn 会合并成一份。要在
  `verify` 脚本里加一份单副本检查。
- 版本：3.0.0（D6，已定）。去掉文件库、9 个工具和 admin API，按 `docs/en/index.md` 的 Versioning 规则属于 Major（公开
  .NET API、`@dignite/ng.site` 导出、HTTP API 都有删除，还有要人工处理数据的迁移）。

验证：只加引用不接线，`dotnet build` 通过；`yarn build` 通过。

### 第 3 步：宿主装 Drive，容器改由 Drive 声明，公开读改成门面

- 宿主 `Dignite.Site.Host`：
  - 依赖 `DriveApplicationModule`、`DriveHttpApiModule`、`DriveEntityFrameworkCoreModule`；`SiteHostDbContext` 调
    `builder.ConfigureDrive()`（是否 `ReplaceDbContext(IDriveDbContext)` 实施时按 Drive host 的写法定），加一个迁移，
    只建 `DriveFileDescriptors`。
  - `ConfigureBlobStoring` 改为 `ConfigureDefault(c => c.UseFileSystem(... App_Data/files))`，与现在同一根目录。
  - `appsettings.json` 加：

    ````json
    "Drive": {
      "Containers": {
        "images":       { "Kind": "UserDrive", "PublicRead": true, "Preset": "Images" },
        "attachments":  { "Kind": "UserDrive", "PublicRead": true, "Preset": "Documents" },
        "theme-assets": { "Kind": "UserDrive", "PublicRead": true, "Preset": "BinaryAssets" }
      }
    }
    ````

    预设的默认值与 Site 现在的策略一致（25 MB、2560×4096；20 MB；5 MB），类型表需逐项核对一次（Drive 的 Documents
    预设含 Archives，Site 的 attachments 也收 ZIP）。有前置改动时，Images 预设默认就把 JPEG 存成 WebP（A3、D8），
    不用写参数；没有时 JPEG 先按原样存（G1）。
- Site：
  - `SiteAdminApplicationModule` 删 `ConfigureFileContainers`、`ConfigurePermissions`。
  - 有前置改动时：删 `JpegToWebpContributor`、`SiteImageStorageConfiguration`、`UprightSkiaSharpImageResizerContributor`、
    移除 ABP 缩放器的那段全局 DI 和 `SkiaSharpResizerOptions` 的配置（都由 A1 的包接手）；`Dignite.Site.Common.Application`
    的 `ImageOrientation` 也删，公开读的 `ImageVariantRenderer` 改用 A1 包里的同名类（`Dignite.Site.Public.Application` 引用
    该包；它本来就直接用 SkiaSharp）。Site 不传任何名字给 Drive。
  - 没有前置改动时：同样删 `JpegToWebpContributor`、`SiteImageStorageConfiguration`（挂不到 Drive 的命名上，见
    「前置」）；`UprightSkiaSharpImageResizerContributor` 和 `SkiaSharpResizerOptions` 的配置**暂留**——它是全局 DI，进程内
    宿主里对 Drive 的缩放同样生效，能挡住「手机照片缩小后横过来」。
  - `Dignite.Site.Public.Application` 引用 `Dignite.Drive.Application.Contracts`，`FilePublicAppService` 改为：先用
    `IDrivePublicAppService.ReadAsync(container, blob, 不带尺寸)` 取原文件，再走 Site 现有的格式协商、缩放和渲染缓存。
    路由、ETag、`download/` 路由不变。过渡期（迁移前）找不到时回退到 Site 旧表，第 8 步删掉回退。
  - `SiteFileUrl` 增加对 Drive 公开地址（`/api/drive/public/{container}/{blob}[?__tenant=…]`）的识别：`Sized`、
    `Absolutize`、`IsSiteFile` 遇到它时改写成 Site 的地址形式（去掉 `__tenant`）。这样新上传的文件即使存的是 Drive 的
    `url`，经 `file_url`、`og:image`、JSON-LD 输出的也是 Site 地址，照样拿到 WebP 和任意尺寸。（D1）
- 验证：`dotnet build`；`Dignite.Site.Public.HttpApi.Client.Tests`、`Public.Web.Tests`、EF Core 测试里的
  `FilePublicAppService_Tests` 改成「Drive 里有文件 → Site 地址能读、能缩放、WebP 协商不变、Drive 地址经 `SiteFileUrl` 改写」；
  第二个 Host 实例上 `curl` 旧地址和新地址，各带 `Accept: image/webp` 与不带。

### 第 4 步：管理后端去掉文件库

- 删：1.1 表中「管理 API」「权限映射」两行列出的全部类、Contracts DTO、HttpApi 控制器、C# 客户端代理（重新生成
  `site-admin` 代理，只保留文件相关文件的删除，见「Worktree host + proxy regeneration」记忆）。
- 写入的一侧也删：`FileDescriptorManager`、`DirectoryManager`、`FileDescriptorSorting`、目录相关的异常，以及
  `SiteDomainModule` 里只为它们注册的 `IMimeTypeDetector` 等。旧实体和仓储的读取方法留到第 8 步（第 7 步说明了原因）。
- 测试：删 `FileAdminAppService_*`、`FileDescriptorAuthorizationHandler_Tests`、`FileContainerPolicy_Tests`、
  `FileDescriptorManager_Tests`、`DirectoryManager*`、`FileDescriptorSorting_Tests`、`FileUploadTicket*` 相关。
- 验证：`dotnet build`、`dotnet test`；grep 不再有 `SiteAdmin.Contents` 被用作文件权限。

### 第 5 步：MCP

- Site 删 1.3 列出的全部文件工具与选项类，`SiteMcpModule` 不再 `AddTools<FileContainerTools/DirectoryTools/FileTools>`，
  不再抬 body 上限。
- `SiteMcpConsts.FileInstructions` 改写：文件用 `drive_*` 工具；放进内容字段、SEO 分享图、模板的是返回的 `url`；
  `drive_list_containers` 先调。另外三处描述里的工具名改成 `drive_upload_file` / `drive_list_containers`。
- 宿主依赖 `DriveMcpModule`（`Dignite.Drive.Mcp`），`BaseUrl` 不设（MCP 与 Drive 同进程）。
- 行为变化要写进 CHANGELOG：内联上传上限从 5 MB 降到 Drive 的 2 MB；上传链接从 `PUT` 原始 body 变成 `curl -F`；
  没有列文件、目录、改名、移动（归 Pro）。
- 验证：`Dignite.Site.Mcp.Tests` 的契约测试与注册测试；MCP Inspector 列工具，确认 `site_*` 文件工具消失、5 个
  `drive_*` 出现，`drive_upload_file` 返回的 `url` 放进一个文件字段后，页面渲染出 Site 地址。

### 第 6 步：Angular

- 文件字段控件：`site-file-control` 改为包一个 `drive-file-input`（`containerName`、`multiple` 来自字段配置），注册键仍是
  `FileExplorer`。旧值（Site 形状的对象）直接喂给它可以显示（`id`/`name`/`url`/`size`/`mimeType` 都在）。
- SEO 字段与 SEO 设置页的分享图：从 `site-file-modal` 改为单文件 `drive-file-input`（D4，已定）。
  - 这两处存的是一个 `url` 字符串，而控件的值是 `DriveFileDto[]`，中间要一层转换：读的时候把已存的地址包成
    `[{ url, name }]`（旧值没有 id；名字取地址最后一段），写的时候取第一个文件的 `url`，移除后存空。
  - `drive-file-input` 不显示缩略图（G6），所以分享图上方的 `site-file-preview` 保留，继续显示当前那张图。
  - D3 的保存后清理在这两处同样生效。
- CKEditor 上传：`SiteCKEditorUploadProvider` 改调 `DriveFileService` 的上传，拿 `url`。
- 删除，按 D3 先做 (b)：
  - 一个按表单划分的清理服务。文件字段和两处 SEO 选图把「本次编辑里新上传、随后又从值里移除」的文件记为候选；内容编辑页、
    SEO 设置页**保存成功后**，对候选逐个调 `DriveFileService.delete`。尽力而为：失败只记日志，不影响保存。
  - 删得动：候选都是当前用户自己上传的，Drive 的规则允许上传者删自己的文件，不需要 `Drive.Files.Delete`。
  - 「新上传」怎么判断：`drive-file-input` 没有「上传了一个文件」的事件，值里只有 `DriveFileDto`。判据是同时满足三条：
    打开编辑时不在值里；`creatorId` 是当前用户；`creationTime` 晚于打开编辑的时刻。装了 Pro 之后，从库里挑出来的文件
    只有「本人在编辑期间、在别处刚上传」这一种会被误判，概率很低。G5 若同时给控件加一个上传事件，可以去掉这个推断。
  - 不处理：CKEditor 正文里插了又删掉的图（要比对 HTML，不在 (b) 范围）；不点保存、直接离开页面时的上传（没有可靠的离开
    事件）。点「取消」关闭编辑时是否也清理，我倾向清理，实施时你看一眼。
  - 验证：vitest 覆盖三种情况——上传后移除再保存会删；保存前已有的文件被移除不删；保存失败不删。浏览器里走一遍，再在
    Drive 表里确认对应行已软删除。
- 删：`src/lib/files/` 下目录树、弹窗、选择器、上传、校验服务、`site-file-containers`；`site-file-preview` 留下（SEO 两处还要用，
  见上）。`public-api` 删除对应导出——对 `@dignite/ng.site` 的使用者是破坏性变更。
- 代理：重新生成后删 `proxy/dignite/site/admin/files/*`。
- 验证：`yarn build` 库与 demo 应用，vitest 全绿；浏览器里走一遍：新建内容上传图片 → 保存 → 公开页显示；
  SEO 分享图；CKEditor 插图；没有 `Drive.Files.Upload` 的用户上传按钮禁用。

### 第 7 步：迁移器（做法见第 4 节，已定）

- 新项目 `src/Dignite.Site.Drive.Migration`（可打包，宿主升级时引用、迁完移除）。它引用 `Dignite.Drive.Domain` 和 Site 的
  EF Core 层，Site 的其他包不引用它。
- 内容：一个数据种子贡献者，按第 4 节第 1–3 条和第 6 条迁移，只支持 EF Core；选项里有 `DryRun`，默认 true，只写报告（行数、
  引用行数、要复制的字节、超长 blob 名、孤儿 blob、目录树）；关掉 `DryRun` 才写入。可以重复执行，已迁的按 `Id` 跳过。目录树
  （含空目录）另存成一个设置值。
- Site 3.0.0 里旧表的 EF 映射保留：`FileDescriptor`、`DirectoryDescriptor` 退成只读的过时实体，只供迁移器读取。第 3 步的门面
  回退也读它们。
- 宿主 `Dignite.Site.Host` 引用这个包，用开发库验证。
- 验证：EF Core 测试准备几种数据——owner 行、引用行、owner 已删只剩引用行、目录与空目录、`MimeType` 为空的行。验证
  dry-run 不写任何东西；执行后 Drive 行的 `Id`/`BlobName`/`DedupKey` 正确；旧地址经门面能读；再跑一次不重复。然后在开发库
  副本上用第二个 Host 实例实跑一遍。

### 第 8 步：删 Site 的文件表（**下一个版本**，不在 3.0.0）

- 原因见第 4 节第 5 条：3.0.0 一去掉映射，宿主升级时生成的迁移就会删表，而迁移器在种子阶段才跑。
- 删 `FileDescriptor`、`DirectoryDescriptor` 及其 EF/Mongo 映射与索引、`FileDescriptorConsts`、`DirectoryDescriptorConsts`、
  门面里的回退，以及 `Dignite.Site.Drive.Migration` 项目；宿主加迁移删 `SiteFileDescriptors`、`SiteDirectoryDescriptors`。
  CHANGELOG 写明：升级到这个版本之前，必须已经在 3.x 上跑完迁移器。
- 测试：删 `FileDescriptorRepository_Tests`、`MongoFileDescriptorRepository_Tests`、迁移器的测试、`TestFiles`；`SiteThemeIndexes_Tests`、`FileField_Tests` 里引用文件表的部分改写。
- 验证：`dotnet build`、`dotnet test`、`dotnet ef migrations add` 只生成两张表的 Drop。

### 第 9 步：文档

- `docs/en/files.md`（已发布，**不改文件名**）重写：文件存在 Drive 里，宿主怎么声明三个容器，权限是 Drive 的，地址不变，
  CKEditor 与 MCP 指向 Drive 文档（绝对链接 `https://…`？Drive 文档还没发布——发布前先不链，写到 CHANGELOG）。
- `docs/en/mcp/tools.md`、`field-types.md`、`settings.md`、`seo/index.md`、`getting-started.md`、`admin-ui.md`、两篇
  rendering 页面里提到文件工具或文件库的段落。
- `dev-docs/site-files.md` 重写为「Site 如何使用 Drive」。
- CHANGELOG、两个 README（绝对链接）。CHANGELOG 的升级说明写明（D5）：文件权限不再跟着 `SiteAdmin.Contents.*` 和
  `SiteAdmin.Themes.Edit` 走，升级后要给编辑内容、编辑主题的角色授 `Drive.Files.Upload`（需要删别人上传的文件时再授
  `Drive.Files.Delete`），授权之前这些角色上传会被拒。
- `docs/en/index.md` 的版本对照表加一行 3.x（D6）：Dignite ABP Modules 的最低版本填 Drive 首次发布的版本，发布时再填。
- 验证：grep 不再出现 `site_list_files` 等旧工具名和 `api/site-admin/files`；docs-nav 不变（不新增页面）。

### 第 10 步：cloud 跟进（不在本仓库，只列出）

SiteService 的 `SiteServiceDbContext` 去掉 `FileDescriptor` 映射、迁移；Drive 装在哪个进程；三个容器的 `Drive` 配置节；
迁移器在生产库上的运行顺序；给编辑角色授 `Drive.Files.*`。

---

## 4. 现有文件的迁移（已定，2026-11-11；只剩第 4 条待 dry-run 结果）

决策记录里只写了目标和难点。下面是我提出的做法，你 2026-11-11 答复「按建议，迁移方案照第 4 节做」（D2）。凡我当时
给了建议的，按建议定；当时没给建议、或要看数据和 D7 才能定的，下面仍标「待定」。abp-modules 决策记录里 "Migration of
Site's files" 还在 "Proposed" 下，要不要在那边改成 "Decided"，属于 abp-modules 的改动，等你发话。

1. **每个活的 Site 行 → 一个 Drive 行，保留 `Id`**。文件字段值里存的 `id` 因此在 Drive 里依然有效，删除照样按 id。
   `Name`、`MimeType`、`Size`、`CreatorId`、`CreationTime`、`TenantId`、`ContainerName` 照搬。软删除的行不迁。
2. **`BlobName` 用 Site 行自己的 `BlobName`**，于是旧地址 `/api/site-public/files/{c}/{BlobName}` 经门面（第 3 步）在 Drive
   里按物理名找得到，存储的地址一个都不用改写。
   - owner 行：物理 blob 已在同名容器里，不搬字节；`ownsBlob = true`（占 `DedupKey`）。
   - **引用行**（地址是自己的，字节在 `ReferBlobName` 名下）：把字节复制一份到它自己的 `BlobName` 下，`ownsBlob = false`。
     代价是重复的那几份字节不再共享；好处是不改任何已存地址、不需要别名表。Drive 的删除逻辑对这种行本来就成立
     （它的 blob 只有它自己在用，删它就删 blob）。
   - owner 已被删、只剩引用行的那组：其中一行占 `DedupKey`，原来那个只剩引用者的物理 blob 复制完后成为孤儿，迁移器
     顺手删掉（按建议定为删）。
   - `Hash`：引用行用其 owner 的 `Hash`；owner 已删的，从字节重新算。`MimeType` 为空的从字节检测。
3. **目录**：开源 Drive 没处放。把每个文件所在目录的路径（目录名逐级，如 `活动/2026`）写进 Drive 行的
   `ExtraProperties["Site:DirectoryPath"]`（`FileDescriptor` 是聚合根，自带 ExtraProperties，不需要 Drive 改表）。
   - 只装开源版：路径存着、不用，不丢。
   - 装了 Pro（迁移时已装或之后再装）：Pro 提供一个导入，把路径变成它的目录树和文件的目录字段。Site 代码在两种宿主里
     一样，不需要认识 Pro。
   - 空目录（没有文件的目录）会丢；若要保住，迁移器把整棵目录树另存一份（例如一个 JSON 设置值）。**已定（2026-11-11，你的回答：
     按建议，存）**。理由：代价很小（一个设置值），而「装了 Pro 的宿主迁移不丢东西」是已定的，空目录也是东西。
4. **超长或不合规的旧 blob 名**（Site 上限 256，Drive 64；旧 File Explorer 时期的名字）：迁移器先出 dry-run 报告；真有这样
   的行，再决定是复制成新名字并改写存储地址，还是另立别名（**待 dry-run 结果**）。
5. **在哪跑、分几次**：迁移要先读 Site 的旧表再删表，所以建议跨两个提交（也可能是两个版本）：第 7 步加迁移器（读旧表、
   写 Drive 行、复制引用行字节，可重复执行、已迁的跳过，先 dry-run 出报告），第 8 步删表。迁移器放在 Site 的哪个项目、
   以什么方式触发（数据种子 / 启动时 / 一次性命令），取决于 D7 的答案：
   - Drive 与 Site 同进程同库（Site 开发宿主、或 cloud 把 Drive 装进 SiteService）：迁移器在进程内直接用 Drive 的仓储写行，
     需要 Site 的迁移项目引用 `Dignite.Drive.Domain`（只在迁移代码里）。
   - Drive 单独成服务：字节要搬到 Drive 服务的存储，行要写进 Drive 的库，而开源 Drive 没有「按指定 id 和物理名导入」的接口
     → 缺口 G4。

   D7 答复后，已知的两个部署（cloud SiteService、Yuantong）都是第一种。由此提出以下建议，**你 2026-11-11 已确认按建议做**：
   - **迁移器做成单独的可选包** `Dignite.Site.Drive.Migration`：宿主升级时引用、迁完移除。Site 的常规包仍只引用 Drive 的
     Contracts，`Dignite.Drive.Domain` 只进这个包。
   - **触发**：包里注册一个数据种子贡献者。cloud 的 SiteService 启动时会自动迁移库并跑种子，Yuantong 走它的 DbMigrator，
     都不用另加命令。默认先 dry-run，只写报告；宿主把选项设成执行，才真正写入。可以重复执行，已迁的跳过。
   - **分两个版本删表**：3.0.0 停用旧表但**保留它们在 EF 模型里的映射**（标为过时，只读）；迁移器读旧表。下一个版本才去掉
     映射，宿主随后生成的迁移会删掉这两张表。原因：宿主的 EF 迁移由宿主自己生成，3.0.0 一去掉映射，宿主升级时生成的迁移就会
     直接删表，而迁移器在种子阶段才跑，那时表已经没了。这与「Site 接入后去掉 `SiteFileDescriptors`」并不冲突：3.0.0 起就
     不再使用它，只是物理删表晚一个版本。第 8 步相应挪到下一个版本。
   - Yuantong 的宿主代码用了 `SiteFileContainerNames.All`。这个常量在 3.0.0 保留（第 3 步只是不再把它当边界），它的升级只需把
     `UseDatabase()` 挪到 Drive 的容器声明上：容器名不变，字节就还在原处。
6. **MongoDB**：Site 支持 Mongo，迁移器若要覆盖 Mongo 宿主，工作量翻倍。已知的部署都是 EF Core；迁移器先只做 EF Core，
   文档里写明（按建议定）。

---

## 5. 开源 Drive 的缺口（不改 abp-modules，只列）

| # | 缺口 | 对 Site 的影响 | 建议 |
|---|---|---|---|
| G1 | Images 预设没有「JPEG 存成 WebP」（Site 在保存前按配置把 blob 名定为 `.webp`，再由处理器转码） | 存储变大；访客不受影响（门面照样按 `Accept` 给 WebP） | 前置 A1–A3：处理器挪进 blob-storing 的新 SkiaSharp 包、Pipeline 声明存储类型、Drive 保存前按存储类型起名、Images 预设加 `StoreJpegAsWebp` |
| G2 | 缩放用 ABP 原版 SkiaSharp 缩放器，超过 2560×4096 的手机照片缩小后会横过来；metadata strip 本身保留了 orientation | 进程内宿主靠 Site 暂留的全局替换挡住；Drive 单独成服务时会出问题 | 前置 A1：「先摆正再缩放」随新包进 blob-storing，Drive 依赖它 |
| G3 | 公开读只认尺寸阶梯、不协商 WebP、无渲染缓存与并发上限、无 `download/` | Site 不能直接用 Drive 地址出图；D1 已定用门面，不阻塞 | 长期可把 Site 的协商与缓存挪进 Drive，届时 Site 可去掉门面 |
| G4 | 没有导入接口（指定 id、物理名、创建者、创建时间，挂到已存在的 blob 上） | Drive 单独成服务时无法迁移 | `IDriveImportService` 或领域层的 `ImportAsync`，只给进程内或受信客户端 |
| G5 | `drive-file-input` 没有删除动作，「移除」只从值里拿掉 | 「当前显示的文件可以删」只能先用 Site 的保存后清理顶着（D3 的 b），删不了以前的文件 | 加一个可选的删除动作（权限：上传者或 `Drive.Files.Delete`），Pro 的替换组件同样照这个输入实现；顺带加一个「上传了一个文件」的输出，Site 就不用靠创建者和时间去推断 |
| G6 | `drive-file-input` 不显示图片缩略图 | 编辑挑分享图、看图片字段只能看文件名，比现在退步 | 公开容器的图片显示缩略图（用 `DriveFileUrlService.sized`） |
| G7 | `DriveContainerDto` 没有用途说明 | `drive_list_containers` 给助手的信息比 `site_list_file_containers` 少（Site 现在写明 images 与 attachments 各放什么） | 容器配置加 `Description`（可本地化），DTO 与 MCP 带出 |
| G8 | `DriveFileDto.url` 带 `?__tenant=`；DTO 没有物理名 | Site 要从 url 里解析 blob 名，耦合到路由格式；地址带租户与 Site「地址不带租户」的取向相反 | DTO 带出物理名（`DrivePublicFileDto` 已有 `ContentId`）；或宿主可选不带 `__tenant` |
| G9 | 文档没说「应用把容器名存进自己数据时，容器名就是契约」 | 宿主改了容器名，Site 的字段配置就指空 | `attaching-files.md` 补一句 |

另有一条**跨产品假设**，不是开源 Drive 的缺口：门面依赖 `PublicRead` 容器在装了 Pro 的宿主里仍可匿名读。决策记录写的是
「Drive Pro, as a drive, normally authorizes a read」，没说 `PublicRead` 是否保留，要在 Pro 的设计里确认。

---

## 6. 需要你拍板的事

- ~~**D1 地址**~~ **已定（2026-11-11，你的回答：按建议用门面）**：保留 `/api/site-public/files/...` 作为 Site 文件的唯一
  对外地址，由门面从 Drive 取字节、保留 WebP 协商与任意尺寸；新文件即便存的是 Drive 的 url，也在输出时改写成 Site 地址。
  未选的另一条路：新文件直接用 Drive 地址、旧地址只做转发（失去 WebP 协商、尺寸受阶梯限制、地址带租户）。
- ~~**D2 迁移**~~ **已定（2026-11-11，你的回答：按建议，照第 4 节做）**。空目录也存（你后来的回答）。剩下两处待定：超长
  blob 名怎么处理（看 dry-run）、迁移器放在哪里怎么触发（看 D7）。
- ~~**D3 删除**~~ **已定（2026-11-11，你的回答：先 b 后 a）**：先做 (b)——Site 在保存成功后删掉本次编辑中「上传了又移除」
  的文件；等 Drive 补上 G5 再做 (a)——控件上直接删。未选的 (c)「保存后删掉所有被移除的文件」有误删风险（同一个文件对象可能
  被复制进别的内容或别的语言版本）。(b) 的做法见第 6 步；(a) 来了以后 (b) 是否保留（它还能清掉没保存就移除的上传），到时再定。
- ~~**D4**~~ **已定（2026-11-11，你的回答：按推断做）**：SEO 字段与 SEO 设置页的分享图也换成 `drive-file-input`，
  做法见第 6 步。
- ~~**D5**~~ **已定（2026-11-11，你的回答：不做）**：不给现有部署做一次性授权，Drive 的权限由管理员自己授。后果是升级后
  编辑在被授 `Drive.Files.Upload` 之前不能上传，所以第 9 步的 CHANGELOG 升级说明要写清楚这一步。
- ~~**D6**~~ **已定（2026-11-11，你的回答）**：`@dignite/ng.site` 把 `@dignite/ng.drive` 放在 `dependencies`；Site 下一版
  是 3.0.0。
- ~~**D7 数据范围**~~ **已答（2026-11-11）**：cloud 只有宿主有文件，Drive 装在 SiteService 进程内；Yuantong 也有文件（同进程、
  SQL Server、`UseDatabase`）。MongoDB 已按 D2 不做。由此带出的第 4 节第 5 条建议，你已确认。
- ~~**D8 前置改动的两处取舍**~~ **已定（2026-11-11，你的回答）**：Drive 的 Images 预设直接带 `StoreJpegAsWebp`
  （SkiaSharp 因此成为每个 Drive 宿主的硬依赖），且默认开。前置改动何时在 abp-modules 开分支，仍等你发话。

---

## 7. 文档与代码不一致

- `SiteAdminApplicationModule.ConfigureContentPermissions` 的注释说「文件随内容权限」出自「总体设计 §6.2.5」
  （`src/Dignite.Site.Admin.Application/SiteAdminApplicationModule.cs:192`），但 `dev-docs/00-总体设计.md` 的 §6.2.5 讲的是
  MCP 的权限与令牌，没有这条。这段代码第 3 步就删，不另外处理。
- `SiteFileUrl` 的注释说旧前缀 `/file-explorer/files/` 「只认一个版本（0.1.0-preview.25），下个版本删」，代码至今仍认
  （`src/Dignite.Site.Domain.Shared/Files/SiteFileUrl.cs:43`）。按代码做：门面改写时一并保留，删不删另议。

---

## 8. 实施记录（2026-11-11，分支 `feat/drive-integration`，未推送）

abp-modules 用 `feat/blob-storing-webp-upright`（A1–A3 已做）整仓打成 `10.7.7-local.1`。每步单独构建、测试、提交：

| 提交 | 内容 |
|---|---|
| `962fdcf` | 本方案 |
| `1a8f84a` | 第 1 步：abp-modules 10.7.6 |
| `8d53b34` | 第 2 步：本地预发布包、`NuGet.Config` 本地源、`@dignite/ng.drive` tgz |
| `013ffeb` | 第 5 步：删 Site 的 9 个文件工具 |
| `5abb046` | 第 4 步：删管理端文件库 |
| `4825a0f` | 第 3 步：宿主装 Drive、公开读改门面 |
| `4dbc05d` | 第 6 步：Angular 改用 `drive-file-input` |
| `8b9a693` | 第 7 步：迁移器 `Dignite.Site.Drive.Migration` |
| （本提交） | 第 9 步：文档、CHANGELOG |

与方案不同、或方案里没写到的地方：

1. **执行顺序是 5 → 4 → 3。** MCP 文件工具直接调管理端文件服务，宿主装 Drive 又会和 Site 的容器策略叠在同一容器上；
   按 3 → 4 → 5 走，中间几步编译不过或测试不过。内容不变。
2. **第 2 步改了仓库的 `NuGet.Config`**（分支上），没有动本机用户级配置：那个文件里有明文凭据，而且用户级 mapping 会影响
   本机所有仓库。Drive 发布后撤掉这个提交和 `file:` 依赖。
3. **`SiteMcpOptions.BaseUrl`**：主题链接原来借用 `SiteMcpFileOptions.UploadBaseUrl`，那个类随文件工具删了，换成新选项。
4. **`common.test.props` 从 `common.props` 读 `DigniteAbpModulesVersion`**：测试项目要直接引用 Drive 包，又不能导入整个
   `common.props`（会带进 Fody、Source Link 和包元数据）。
5. **测试辅助 `LegacySiteFiles`**：旧库删掉后，测试回退路径和迁移器要能造旧数据；`images` 里的 JPEG 按 10.7.5 以来的实际
   行为存成摆正的 WebP。
6. **字段里存什么地址**：控件存 Drive 返回的 `DriveFileDto` 原样（`url` 是 Drive 地址、带 `__tenant`），输出时由
   `SiteFileUrl.Normalize` 改写成 Site 地址（D1 的「输出时改写」）。这样 `drive-file-input` 里的链接在多租户下也能打开。
   CKEditor 存的是 Site 地址（它本来就把地址转成相对的 Site 地址）。
7. **D3 (b) 的判据**按第 6 步写的做：创建者 + 创建时间 + 编辑前不在值里；保存后对照「保存的值」序列化文本里有没有 id 或
   BLOB 名，而不是对照各控件当前值——Matrix 块折叠会销毁子控件，按控件统计会误删。**「取消/离开时清理」没有做**，等你定。
8. **空目录的保存方式没有落地。** 方案写的是「另存成一个设置值」，实施时发现 ABP 设置值上限 2048 字符，目录多时放不下；
   现在迁移器把每个文件的目录路径写进 `ExtraProperties["Site:DirectoryPath"]`，整棵目录树（含空目录）只写进迁移报告和日志，
   没有持久化。持久化方式待你定。
9. **迁移器复制字节绕过容器流水线**（直接用存储 provider），否则旧 JPEG 会被再转一次 WebP；孤儿 blob 在提交后才删。
10. **门面只处理进程内 Drive 的「找不到」**（`EntityNotFoundException`）；Drive 单独成服务时远程调用的 404 是另一种异常，
    没处理也没测。已知部署都是进程内。
11. **界面实测没做完**：浏览器面板隐藏、`localhost:4200` 导航被拒，Angular 部分只有构建和 191 个单元测试为证。后端（上传、
    WebP、缩放、旧地址回退、迁移）在第二个宿主实例上实测过。
