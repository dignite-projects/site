# 主题：工作副本与发布快照

> 状态：2026-11-01 定稿。随 2.0.0 一起发布：2.0.0 从未正式对外发布，这里的改动沿用 2.0.0 版本号，不另开大版本。
> 第一个 PR 已实现领域、存储、渲染、校验、管理 API 与 MCP（§11 分期），第二个 PR 实现管理端工作台，第三个 PR 实现
> 浏览器内预览（§7）；字段侧栏待后续 PR。
> [00-总体设计](00-总体设计.md) §7.2、§7.4、§7.5 已按本文改写。
> 逐模板版本、逐模板发布、使用者计数在这里被推翻，#108、#109 不合并，只搬可复用的部分（见 §9）。

## 1. 要解决的问题

现在的模板系统以**单个模板**为单位记版本、发布、预览，实际用下来有五个问题：

1. **发布不是原子的。** 改 layout 加一个 partial 要发布两次，两次之间线上是半新半旧的组合，可能直接渲染失败。
2. **版本历史是噪音。** 每次保存都是一个版本；加上 ABP `FormSubmitDirective` 把编辑器里的回车变成提交，
   一次编辑能产生几十个版本。历史里看不出"这次上线改了什么"。
3. **只有一个 layout。** 名字写死为 `layout`。小型站点就会需要"首页满宽 / 文章页带侧栏 / 落地页无导航"。
4. **目录结构由程序规定。** `include` 只能读 `partials/`，资源只能放 `assets/`，作者没法按自己的习惯组织文件。
5. **使用者计数没有用处。** 每次打开列表都把所有模板解析一遍，换来的数字回答不了作者真正的问题：
   从哪个页面开始预览、能不能删、发布后会不会断。

## 2. 参考了谁

| 来源 | 许可证 | 借鉴 | 不采纳 |
|---|---|---|---|
| [Ghost / Casper](https://github.com/TryGhost/Casper/blob/main/post.hbs) | MIT | 每个模板首行写 `{{!< default}}` 声明自己套哪个 layout | 没写就不套、没有默认 layout：每个页面模板都要多写一行 |
| [Shopify 主题](https://shopify.dev/docs/storefronts/themes/architecture/layouts)（闭源，只看公开设计） | — | 整套主题是发布单元；可以有多个 layout，模板用 `{% layout 'x' %}` / `{% layout none %}` 自选，不写就用默认 layout；草稿主题整体预览 | 每文件的 Timeline 历史：只能逐个文件恢复、资源文件没有历史、会过期，[社区里](https://community.shopify.com/t/how-to-undo-liquid-code-changes-that-messed-up-your-online-store/53769)的通行建议是"改之前先复制整套主题"；固定目录；默认 layout 是写死的文件名 `theme.liquid`，我们改为设置项 |
| ASP.NET Core Razor 视图约定 | MIT | layout 不是一种文件，被 `Layout = ...` 指向才是 layout；`_` 开头表示局部视图只是习惯；目录由开发者定 | 用 `_ViewStart` 文件按目录逐级指定默认 layout：我们只要一个主题级的设置项 |
| [Umbraco 模板](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/templating-and-rendering/templates.md) | MIT | 子模板声明 `Layout` | 嵌套 layout：先不做（§10） |
| ABP TextTemplating 10.5（本机 NuGet 缓存） | LGPL-3.0 | `TemplateDefinition.Layout`：由模板定义指向 layout | 定义写在代码里，不适用于租户数据 |
| Scriban 7.2.1（本机 XML 文档） | BSD-2 | `ScriptMode.FrontMatterAndContent`：模板头部的 front matter 单独成一棵语法树、渲染时不执行，可以静态读出 | — |

结论：**"模板自己声明 layout"是共识**，不声明时 Shopify 用默认 layout、Ghost 不套，我们取 Shopify 的做法，但默认 layout
是设置项而不是写死的文件名；**"整套发布"是 Shopify 的做法，逐文件历史在 Shopify 是公认的短板**；
**layout 由引用决定、目录由作者决定**是 ASP.NET Core 的做法。

## 3. 概念

```
主题 (SiteTheme，每租户一个)
├─ 工作副本：一组文件，随存随改，没有版本；新主题默认建好两个推荐目录
│    layouts/default    layouts/landing
│    assets/site.css    assets/js/menu.js
│    Events   News   blog/post   _card   blog/_sidebar     ← 其余由作者自己组织
└─ 发布快照 (SiteRelease #1, #2, ...)：某一刻整套文件的冻结副本，不可变
       ↑ 线上指针 LiveReleaseId
```

- **保存**只覆盖工作副本里的一个文件。不产生版本，不影响线上。
- **发布**把整个工作副本冻结成一个新快照，并把线上指针指向它。版本历史就是快照列表。
- **回滚**把线上指针指回旧快照，工作副本不动。另有"取回到工作副本"，把旧快照的文件覆盖回工作副本继续改。
- **预览**看的是工作副本整体，或某个快照整体，不再有"把某个模板的某个版本替换进线上"的规则。

## 4. 文件

### 4.1 程序不认任何目录

程序里没有保留目录。新主题默认建好 `layouts/` 和 `assets/` 并放进骨架文件，这只是推荐的组织方式，作者可以改名、删掉、另建。

文件只分两种，由**扩展名**决定：

| 文件名 | 是什么 | 怎么用 |
|---|---|---|
| 不带扩展名，如 `Events`、`_card`、`layouts/default` | Scriban 模板 | 按被谁引用决定怎么渲染，见下 |
| 带静态资源白名单里的扩展名，如 `site.css`、`menu.min.js` | 静态资源 | 不当 Scriban 解析，由 `/site-assets/{完整路径}` 原样输出，`Content-Type` 按扩展名 |
| 带其他扩展名 | — | 保存时拒绝 |

模板不带扩展名，引用时写的名字就是文件名，`Page.Template` 也不用改。代价是以后若要把主题导出到磁盘或与 git 同步，
导出时需要给模板临时加一个扩展名，编辑器才认得出；这一点留到真做导出时再定（曾考虑过 `.tpl`、`.sbnhtml`，用户 2026-11-01 决定不要）。

一个 Scriban 模板在一次渲染里扮演什么角色，看它被谁引用：被 `Page.Template` 指向就是页面模板，被 `include` 就是片段，
被页面模板的 front matter 指向就是 layout。程序不给文件标类型，界面上也不标；`_` 开头表示片段只是推荐的习惯，程序不检查。

- **静态资源扩展名有白名单**，默认就是现在 `Content-Type` 表里的那些：`css`、`js`、`mjs`、`json`、`map`、`txt`、`svg`、`xml`、
  `webmanifest`、`ics`、`csv`，宿主可以通过选项增加。`.html`、`.sbn` 这类不在名单里的扩展名保存时拒绝，提示"Scriban 模板不带扩展名"，
  免得作者得到一个被原样输出、从不执行的"模板"。
- 只有最后一段可以带点，扩展名取最后一个点之后；目录名不带点；`..`、以 `.` 开头的段照旧禁止。
  `Page.Template` 本来就不允许点，所以页面永远指不到静态资源。
- **所有引用一律写从根开始的完整路径**：`include "blog/_sidebar"`、`layout = "layouts/landing"`、`asset_url "assets/site.css"`。
  不做"先找当前目录、再找共享目录"的查找，引用写什么就是什么。
- 资源地址带上完整路径，用推荐目录时是 `/site-assets/assets/site.css?v=…`，前缀和目录名重复一次。这是不保留目录的代价，
  不另做路径改写。
- `Page.Template` 的值就是模板路径，比如 `blog/post`；工作副本或线上快照里没有这个文件时，交给同名 Razor 视图，同现在。

### 4.2 页面模板声明 layout

用 Scriban 自带的 front matter，标记为 `+++`：

```
+++
layout = "layouts/landing"
+++
<article>{{ content.fields.title }}</article>
```

- `layout` 写完整路径，和其他引用一致。
- **不写 `layout` 就用主题的"默认 layout"设置**。这是主题上的一个设置项，不是写死的文件名，新主题初始值为 `layouts/default`，
  作者可以改成任何模板路径，也可以清空。清空后，不写 `layout` 的页面就不套 layout。
- `layout = "none"`：明确不套 layout，模板自己输出完整文档，自己调用 `site_head`。
- 默认 layout 和文件一样属于工作副本，随发布冻结进快照（§6）。改它要发布才上线，预览工作副本时看到的是改后的值，
  回滚时一起回到旧值。否则改一下设置就能绕过"整套发布、整体预览"，等于又回到逐项上线。
- front matter 只允许**字面量赋值**，保存时检查。front matter 不在渲染时执行，服务端用语法树读出值；
  这样校验和预览都不必渲染就知道每个页面用哪个 layout。
- 键名先只开放 `layout`。别的键一律报错，以后要加（比如 `content_type`，见 §10）不会和存量模板冲突。
- front matter 只在作为页面模板渲染时生效，被 `include` 或作为 layout 时忽略。layout 套 layout 先不做。

### 4.3 新建文件

新建对话框只有一个路径输入框，提示里写一句"片段建议以 `_` 开头"。骨架按路径给（#109 的 `skeletonFor` 改写）：
静态资源为空；在 `layouts/` 下给 layout 骨架；名字以 `_` 开头给片段骨架；其余给页面模板骨架，不写 front matter，用默认 layout。
编辑器的语法高亮按扩展名选：模板用 HTML，`.css`、`.js` 等用各自的语言。
骨架只是起点，按推荐目录猜的，不代表类型。

## 5. 编辑、校验与发布

### 5.1 保存

- 工作副本**允许保存有语法错误的文件**。工作副本只有作者看得到，拒绝保存只会丢掉写到一半的工作。
  错误照常列在编辑器旁，文件树里该文件标红。扩展名不在白名单、路径不合法这类错误仍然拒绝保存。
- 每个文件带 `ConcurrencyStamp`。两个人同时改同一个文件，后保存的人得到冲突提示，可以重新载入或强制覆盖。
- 文件树对照线上快照给每个文件标状态：未改、已修改、新增、已删除。比较的是内容哈希，不用读正文。
- 重命名是一次操作：改路径，并列出引用旧路径的文件，由作者决定是否一并替换引用。

### 5.2 校验

一个函数，三处用：编辑器里的"检查"按钮、发布对话框、MCP 的 `site_check_theme`。范围是工作副本，文件数是几十个量级，现算即可。
文件的角色在这里从引用关系里算出来，不需要提前给文件定类型。

| 级别 | 内容 |
|---|---|
| 错误，阻止发布 | 语法错误；front matter 有非字面量或未知键；默认 layout 指向不存在的文件或静态资源；引用了不存在的文件（只认字面量引用，同现在的语法树遍历）；`include` 或 `layout` 指向静态资源；`asset_url` 指向 Scriban 模板；循环 include；被选作 layout 的文件没调 `site_head`；被页面指向、又不套 layout 的模板没调 `site_head` |
| 提示，不阻止 | 页面的 `Template` 在工作副本里没有对应模板，这些页面将由 Razor 视图渲染；被选作 layout 的文件没用 `body`；同一个文件既被选作 layout 又被页面指向或被 `include`；既没有页面指向、也没有被引用的文件 |

列表里不再显示使用者数量，原来的"由 Razor 渲染的页面"卡片并入这里。

### 5.3 删除

删除文件前查一次工作副本里谁引用它、哪些页面指向它。有就列出来，要求确认。这是使用者信息唯一保留的用途。

### 5.4 发布

- 发布对话框显示：校验结果、相对线上快照改动的文件列表（可逐个看差异）、默认 layout 有变化时的新旧值、说明输入框。
- 对话框打开时记下工作副本的整体指纹（各文件哈希加默认 layout 的哈希）。点发布时带上它，服务端发现工作副本在这期间被改过就拒绝，
  保证上线的就是作者审过的那一版。
- 发布在一个事务里完成：写快照、写快照文件清单、写正文、移动线上指针。

### 5.5 快照历史

列出编号、说明、发布人、时间、改动文件数。每个快照可以：设为线上（回滚）、取回到工作副本、和工作副本或别的快照比较。
不设保留上限；正文按哈希去重（§6），同一个文件不变就不重复存。

## 6. 数据模型

| 实体 | 字段 | 说明 |
|---|---|---|
| `SiteTheme`（聚合根，每租户一个） | `Id`、`TenantId`、`DefaultLayout?`、`LiveReleaseId?`、`ConcurrencyStamp` | `DefaultLayout` 是工作副本的设置，新主题为 `layouts/default`；预留多主题时一个租户多行 |
| `SiteThemeFile`（聚合根） | `Id`、`ThemeId`、`Path`、`Body`、`ContentHash`、`ConcurrencyStamp`、审计字段 | 工作副本，一个文件一行；保存只更新这一行 |
| `SiteRelease`（聚合根，不可变） | `Id`、`ThemeId`、`Number`、`DefaultLayout?`、`Comment`、`CreationTime`、`CreatorId` | `DefaultLayout` 是发布那一刻的设置，线上渲染读这里 |
| `SiteReleaseFile`（属于 `SiteRelease`） | `ReleaseId`、`Path`、`ContentHash` | 快照清单，不存正文 |
| `SiteThemeBlob` | `TenantId`、`ContentHash`、`Body` | 发布过的正文，按哈希去重；只在发布时写入 |

**资源地址改用内容哈希**：`asset_url "assets/site.css"` 输出 `/site-assets/assets/site.css?v=<哈希>`。
同一个文件内容不变，跨快照地址不变，浏览器和 CDN 缓存能复用；内容变了地址必变，`immutable` 照旧成立。
现在用版本 Id 是为了防"删除重建后版本号重排"，内容哈希天然没有这个问题。
`/site-assets/` 只输出带扩展名的文件，只从 `SiteThemeBlob` 取正文，所以 Scriban 模板的源码和草稿都不会被直接下载；
草稿资源只在预览态下从工作副本取。

**渲染缓存按快照 Id**：快照不可变，整套文件和解析后的模板可以长期缓存，不需要失效。Public.Web 按租户缓存
最近一次的线上快照（`SiteLiveThemeCache`），每次请求带着它的 Id 问一次 `GetLiveAsync(knownReleaseId)`：还是它就只回一个 Id，
换了才回整套文件。所以每次页面请求只多一个按主键取 `LiveReleaseId` 的查询，发布或回滚在下一个请求、在每个实例上立即生效，
不需要分布式缓存和失效通知。解析后的模板按"内容哈希 + 路径"缓存（`ScribanTemplateCache`），快照之间没变的文件共用。

MongoDB 提供程序现在就没有实现模板仓储，这次同样不实现，文档里照旧写明。

## 7. 预览

沿用 #108 的令牌换 cookie、横幅、`noindex` / `no-store`、退出链接、无效令牌按普通访客处理，只改令牌载荷：

- `{ t: 租户, r: "" }`：预览工作副本。每次请求读工作副本的当前内容，作者保存后刷新预览窗口就能看到。
- `{ t: 租户, r: 快照 Id }`：预览某个历史快照，回滚前先看一眼。

横幅写"正在预览工作副本"或"正在预览发布 #12"；要预览的快照已不在时写明"显示的是线上发布"。工作副本只经进程内钩子
`ISiteThemePreviewSource` 读取，公开 API 不暴露工作副本。Data Protection 的 purpose 换成新的
（`Dignite.Site.ThemePreview`），cookie 改名 `Dignite.Site.Preview`，#108 的令牌一律读不出来。

起始页面：工作台的"预览"默认打开首页。编辑的文件有页面指向时多一个下拉，列出 `Template` 等于该路径的启用页面；
路由的第一个参数可选时（`/events/{slug?}`）打开它前面的字面段，第一个参数必填时（新闻详情）没有自己的地址，
下拉里置灰并提示从预览里点进去。原计划的"带 slug 的页面再选一条内容"没做：管理端内容没有站点地址，拼地址要把
路由展开搬到前端，不值。页面列表要 `SiteAdmin.Pages` 权限，没有时下拉不出现。

## 8. 管理端与 MCP

### 8.1 管理端：主题工作台

替代现在的"模板列表页 + 单模板编辑页"：

- 左：文件树，按作者的目录展开，带状态标记，可新建、重命名、删除。不标文件类型。
- 中：编辑器，多标签，Ctrl+S 保存，离开前提示未保存。**不放在 `<form (ngSubmit)>` 里**，回车问题随之消失。
- 右：有页面指向的文件显示字段侧栏（#109 搬过来）；其余显示校验结果。
- 顶部：检查、预览站点、发布、快照历史、主题设置。主题设置目前只有"默认 layout"一项，从工作副本的模板里选，可清空。

页面表单的模板下拉列出工作副本里的模板，仍可手填 Razor 视图名（#109 的 datalist 搬过来）。

权限拆成 `SiteAdmin.Themes`（看）、`.Edit`（改工作副本）、`.Publish`（发布与回滚）。

### 8.2 MCP

| 工具 | 作用 |
|---|---|
| `site_list_theme_files` | 文件路径、相对线上的状态 |
| `site_get_theme_file` | 读工作副本里的一个文件 |
| `site_save_theme_file` | 写一个文件，返回该文件的语法错误 |
| `site_delete_theme_file` | 删除，返回引用者和指向它的页面 |
| `site_update_theme` | 改工作副本的默认 layout（读在 `site_list_theme_files` 的返回里） |
| `site_rename_theme_file` | 改路径，默认一并改写字面量引用 |
| `site_restore_release` | 把某个快照取回工作副本 |
| `site_check_theme` | §5.2 的校验 |
| `site_preview` | 按路径用工作副本渲染出 HTML（取代 `site_preview_template`） |
| `site_publish_theme` | 发布，需要 `.Publish` 权限 |
| `site_list_releases` / `site_set_live_release` | 历史与回滚 |

AI 先写文件、再检查、再预览、最后发布，正好是工作副本模型的自然顺序。

## 9. 版本号与已有数据

- **版本号**：2.0.0 没有正式发布过，仓库里也没有 `v2.0.0` 标签，所以这套改动仍是 2.0.0。CHANGELOG 里 `[2.0.0]` 条目中
  关于模板的部分按新设计重写，日期改为真正发布的那天。
- **代码**：`SiteTemplate` / `SiteTemplateVersion` 及其表、管理 API、MCP 工具、权限名直接替换，不保留兼容层；
  宿主的 EF 迁移新增一个，删旧表、建新表。
- **已有数据**：只有开发库里有，用一次性的转换工具转，不进发布的代码：

  | 现在 | 新 |
  |---|---|
  | `layout` | `layouts/default`，默认 layout 设置为它；原来没有 layout 时设置为空 |
  | 页面模板 `X`、`partials/x`、`assets/x.css` 等 | 路径不变，正文不用改写 |
  | 每个模板的最新版本 | 工作副本里的文件 |
  | 每个模板的已发布版本 | 快照 #1，说明写"转换自旧模板" |
  | 其余历史版本 | 丢弃 |

- **文档**：`docs/en/rendering/scriban-templates.md` 按新设计重写，文件名不变；docs.dignite.com 的 latest 读 main，
  这一页合并即更新。

**从 #108、#109 搬过来的**：Public.Web 的预览令牌、cookie、横幅、响应头、退出端点；Admin `ContentDto.Url` 填充；
页面表单的模板 datalist；字段侧栏与 `snippetFor`、`skeletonFor`；`ScribanTemplateSyntax.GetTemplateReferences`。
**不搬的**：使用者计数与引用图、逐模板版本覆盖规则、预览链接的版本号参数、`SiteTemplateKind` 枚举和类型列。

## 10. 先不做，但结构上留口

- **多套主题**：`SiteTheme` 已是独立实体，以后一个租户多行、线上指针挪到租户级即可，像 Shopify 那样复制一套去试改版。
- **嵌套 layout**：front matter 允许 layout 也声明 layout。
- **`content_type`**：给不套 layout 的 RSS、sitemap 类输出声明响应类型。
- **定时发布**：快照已经存在，只差一个"在某时刻移动指针"的后台作业。

## 11. 已定

| 事项 | 结论 |
|---|---|
| 目录 | 程序不认任何目录；新主题默认建 `layouts/`、`assets/` 作为推荐（用户 2026-11-01） |
| 类型 | 带扩展名为静态资源，扩展名有白名单；其余为 Scriban 模板，角色由引用决定，界面不标类型；`_` 开头表示片段只是推荐（用户 2026-11-01） |
| layout 引用 | front matter 写完整路径 `layout = "layouts/landing"`（用户 2026-11-01） |
| 默认 layout | 主题设置项，初始值 `layouts/default`，可改可清空，随快照冻结（用户 2026-11-01） |
| 模板扩展名 | 模板不带扩展名（用户 2026-11-01，否决了 `.tpl`、`.sbnhtml`） |
| 版本号 | 沿用 2.0.0（用户 2026-11-01） |
| 保存 | 工作副本允许保存有语法错误的文件，只拦发布 |
| 快照保留 | 不设上限，靠哈希去重控制体积 |
| 分期 | 三个 PR，都进 main：①领域、存储、渲染、校验、管理 API 与 MCP，并删掉旧的管理端模板界面（MCP 工具只是应用服务的薄封装，不跟着改就编译不过；旧界面调用的端点已不存在，留着只会报错）；②管理端主题工作台；③浏览器内草稿预览（令牌换 cookie、横幅）与字段侧栏 |
