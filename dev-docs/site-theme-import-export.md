# 主题导入与导出

> 状态：2026-11-05 用户审过并实现（分支 `feature/seo-and-theme-packages`）。是 [site-themes](site-themes.md) 的续篇，补上它
> §4.1 留下的"导出到磁盘"。导入导出单独成一个应用服务 `ISiteThemeTransferAdminAppService`，不加到 `ISiteThemeAdminAppService`
> 上，免得动它的 C# 客户端代理；新服务的 C# 和 Angular 代理都由 `abp generate-proxy` 生成。
> 和 [site-seo](site-seo.md) 一起发 2.2.0。

## 1. 要解决的问题

主题现在只能在管理端编辑器里、或经 MCP 一个文件一个文件地改。有三种用法做不到：

1. **样式在本地构建。** 页面先在本地做静态原型，用 Tailwind 构建出 CSS，再放进主题。Tailwind 构建时还要扫描主题里的模板：
   原型里没有的类名（分页、表单报错、空列表这些状态，以及没有原型的页面）只出现在模板里。所以要能把主题拉到本地，再把 CSS 推回去。
2. **批量放入。** cloud 官网有 61 个 Razor 视图要转成模板。逐个调用 `site_save_theme_file`，每个文件的正文都要经过模型；
   实际踩过，模型重打长正文会混进形近字。
3. **留存。** 把某个发布或工作副本存进 git，作为站外备份或对比用。

## 2. 参考了谁

| 来源 | 许可证 | 借鉴 | 不采纳 |
|---|---|---|---|
| [Ghost](https://github.com/TryGhost/Ghost) Admin API 上传主题 | MIT | 整套主题打成 zip 上传；上传时先校验（gscan） | 上传后可直接激活：这里只进工作副本，发布照旧先检查 |
| Shopify CLI `theme pull` / `theme push`（闭源，只看公开文档） | — | 拉、推整套或部分文件（`--only`）；推送默认删除远端多余的文件，`--nodelete` 关掉 | `theme dev` 监听本地文件热同步 |
| Site 自己的文件上传链接（2.0，`docs/en/files.md`） | — | 一次性链接：Data Protection 签名、10 分钟、只能用一次、以签发者身份执行；返回 sh 和 PowerShell 两种命令 | — |

## 3. 设计

### 3.1 包

- 一个 zip。条目路径就是主题里的路径，模板照旧没有扩展名。site-themes §4.1 讨论过导出时给模板临时加扩展名，用户否决了
  `.tpl`、`.sbnhtml`；不加扩展名，导出和导入能原样往返。编辑器里要高亮，可以在本地编辑器里把这个目录下的无扩展名文件关联为 HTML。
- 根目录多一个 `.site-theme.json`，记录默认布局页，以及导出自哪里（工作副本或发布 #N）、导出时间。主题路径的段不允许以 `.` 开头，
  这个名字不会和主题文件撞上。
- 不放二进制文件。主题不收二进制，字体和图标走 `theme-assets` 文件容器（2.0）。

### 3.2 导出

- 来源是工作副本（默认），或者某个发布。
- `GET api/site-admin/theme/export?releaseId=...` 直接返回 zip，需要 `SiteAdmin.Themes`。
- 给 MCP 和脚本用的一次性下载链接：`POST api/site-admin/theme/download-tickets` 签发，`GET api/site-admin/theme/download/{ticket}` 下载
  （匿名，链接就是凭据）。MCP 工具 `site_create_theme_download_url` 返回链接和下载命令，zip 由客户端直接存到磁盘，不经过模型。

### 3.3 导入

- 一次性上传链接：`POST api/site-admin/theme/upload-tickets` 签发，`PUT api/site-admin/theme/upload/{ticket}`，请求体是 zip。
  MCP 工具 `site_create_theme_upload_url`。管理端的导入按钮直接带登录状态上传，不用链接。
- **只进工作副本，不发布。** 返回新增、修改、删除、未变的文件列表，以及每个文件的语法错误（同保存）。之后照常检查、预览、发布。
- **两种方式，签发链接时选定：**
  - **合并**（默认）：包里的文件新增或覆盖，包里没有的不动。只推 `css/site.css` 就用这种。
  - **替换**：工作副本变得和包完全一样，包里没有的文件删掉，同"载入编辑"。
- 包里有 `.site-theme.json` 时按它设默认布局页；没有就不动。
- **整包校验，有一处不合格就整包拒绝，并列出全部问题：**
  - 路径规则与保存相同：段规则、静态资源扩展名白名单、语言扩展名。
  - 不能有 `..`、绝对路径、反斜杠、符号链接，防止解压到包外（zip slip）。
  - 单个文件不超过 256 KiB（同保存）；文件数和解压后总大小有上限，防止压缩炸弹，上限做成选项。
  - 必须是 UTF-8 文本。
- 在一个工作单元里全部写入，要么全成，要么全不成。
- 不检查并发标记。和"载入编辑"一样，导入会覆盖别人刚保存的同名文件，返回结果里列出被覆盖的文件。

### 3.4 权限与部署

- 导出需要 `SiteAdmin.Themes`，导入需要 `SiteAdmin.Themes.Edit`。签发时检查一次，用链接时再以签发者身份检查一次，同文件上传链接。
- 链接的地址开头和文件上传链接用同一个来源（`SiteMcpFileOptions.UploadBaseUrl` 或 MCP 请求进来的地址）。网关要放行
  `api/site-admin/theme/upload/*` 和 `api/site-admin/theme/download/*`。

### 3.5 管理端

主题工作台的工具栏加"导出"（导出工作副本）和"导入"（选 zip 和方式，结果用列表显示）；发布历史每一行加"导出"。

## 4. cloud 的用法（参考，不属于 Site）

`website-design/dignite.com` 加两个脚本：

1. **拉取**：要一个下载链接，把工作副本解压到 `theme/`，提交进 git 作为快照。
2. **推送样式**：Tailwind 同时扫描原型 `*.html` 和 `theme/`，构建出 `css/site.css`，打成只含这一个文件的 zip，按合并方式导入，再检查、发布。

`input.css` 里的 `url(../fonts/...)`、`url(../images/...)` 要改成绝对地址：CSS 改从 `/site-assets/css/` 提供后，相对地址会指错。

## 5. 不做

| 事项 | 原因 |
|---|---|
| 导入后直接发布 | 发布必须先过检查和试渲染（site-themes §5） |
| 本地文件变动自动同步（Shopify `theme dev`） | 拉取、推送两个动作已够用，自动同步要常驻连接 |
| 包里带二进制文件 | 主题不收二进制，见 3.1 |
| 导出时给模板加扩展名 | 用户 2026-11-01 否决过扩展名；原样往返更简单 |

## 6. 版本

新增 API、MCP 工具和管理端按钮，没有数据库迁移，随 2.2.0。
