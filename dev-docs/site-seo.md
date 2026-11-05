# SEO：head 元数据与结构化数据

> 状态：2026-11-05 用户审过并实现（分支 `feature/seo-and-theme-packages`）。起因是 cloud 官网从 Razor 视图迁到 Scriban 主题（见 §7）。
> 实现与草稿不同的地方见 §8。
> 改写 [00-总体设计](00-总体设计.md) §5.3、§5.4 的部分结论，理由见 §3。
> 随主题导入导出（[site-theme-import-export](site-theme-import-export.md)）一起发 2.2.0。

## 1. 要解决的问题

cloud 官网的 Razor 布局在 `<site-head-metadata>` 之前改写 `HeadMetadata`，之后再补标签。Scriban 模板跑在沙箱里，
拿不到 C#，这些做不到：

1. **分享图的回退。** 没有分享图的页面用站点默认图；博客用封面当分享图，同时把 Twitter 卡片改成大图。
   模板改不了 `site_head` 已经写出的 `og:image`、`twitter:card`，自己再写一条就重复了。
2. **补充的 Open Graph 标签。** `og:site_name`、`og:locale` 和 `og:locale:alternate`、默认图的 `og:image:width/height`。
3. **JSON-LD。** cloud 的 `Seo/StructuredData.cs`（270 行）输出 Organization、WebSite、BreadcrumbList、BlogPosting、
   SoftwareApplication + AggregateOffer、Service、FAQPage。Scriban 里用 `object.to_json` 加 `html_raw` 也能拼，但每个主题、
   每个写主题的 AI 都要自己保证三件事：转义（#20 记过，正文里的 `</script>` 是存储型 XSS）、地址补成绝对地址、去掉空值。
4. **标题和描述的长度。** cloud 的 `SeoText` 在渲染时拼站名、按显示宽度截短描述。

前三项要么由 Site 做进平台，要么每个主题各写一遍；第 4 项见 §4.6 和 §6。

## 2. 参考了谁

| 来源 | 许可证 / 最近提交 | 借鉴 | 不采纳 |
|---|---|---|---|
| [Jekyll SEO Tag](https://github.com/jekyll/jekyll-seo-tag)（`lib/template.html`） | MIT / 2026-09-29 | 一个标签写出全部 head 标签和 JSON-LD；站点级默认值（默认图、站名）；`og:locale` | `<title>` 由标签写；按"有没有日期"猜 BlogPosting / WebPage；`rel=prev/next` |
| [Ghost](https://github.com/TryGhost/Ghost)（`ghost/core/frontend/meta/og-image.js`、`schema.js`） | MIT / 2026-11-05 | 分享图回退顺序：内容自己的分享图 → 封面 → 站点默认图 | 按页面上下文自动生成 Article / WebSite / Person；自动摘正文当描述 |
| [Orchard Core SEO](https://github.com/OrchardCMS/OrchardCore)（`OrchardCore.Seo`：`SeoMetaPart`、`SeoAspect`） | BSD-3-Clause / 2026-11-04 | 算好的元数据对象带 `og:site_name`、`og:locale`、图片 alt；按内容类型开关可编辑项 | 每条内容存一段原始 JSON-LD；OG、Twitter 各自一套标题、描述、图 |
| [Unhead Schema.org](https://github.com/unjs/unhead)（`docs/schema-org`） | MIT / 2026-11-05 | 节点由前端代码声明，库负责补全地址、去重、输出成一张 `@graph` | 库按节点类型补默认值、自动连 publisher：那是在替前端选形状 |

结论：几家都把"站点默认值 + 内容覆盖 + 回退顺序"放在平台里。分歧在 JSON-LD：Jekyll、Ghost 由平台按页面类型生成，
Orchard 让编辑手写 JSON，Unhead 由前端声明、库负责序列化。Site 取 Unhead 的分工，理由见下节。

## 3. 和 #20、#40、总体设计 §5.4 的关系

#20 曾在后端生成 JSON-LD（`ContentType.SchemaType`、`ContentTypeField.SchemaProperty`、Schema.NET），#40 整个撤掉，理由是：
Site 是 CMS，后端只负责交出数据；JSON-LD 选什么类型、字段怎么对应，是渲染层的决定，而前端不锁语言（总体设计 §7.1）。

本设计守住这条线：**Site 不存 schema 映射，也不替模板选类型。** 变的只有两处：

- **Site 负责序列化正确。** 模板给出节点，Site 负责输出：转义、补绝对地址、去空值、合成 `@graph`。#40 承认的代价之一是
  "每种前端各自重新实现序列化正确性"；Scriban 主题跑在 Site 里面，这份代价由 Site 一次付掉。外部前端不受影响。
- **Site 交出面包屑数据。** 面包屑由页面树派生，和 canonical、hreflang 是同一类，属于 §5.3 的"路由派生的产物"。

总体设计 §5.4 改为："Site 不决定 JSON-LD 的形状；提供面包屑数据和安全的输出通道"。§5.3 里"标签输出复用 `Dignite.Abp.Seo`"
早已不符合代码（`Dignite.Site.Public.Application` 直接引用 `SeoTags`），一并改正。

## 4. 设计

### 4.1 新增设置

| 设置 | 默认 | 说明 |
|---|---|---|
| `Site.Seo.DefaultShareImage` | 空 | 页面没有分享图时用的图。文件库地址或绝对地址。 |

站名用现有的 `Site.Branding.AppName`。只加这一项，因为 Site 只在派生分享图时需要它；其余站点级信息（站点身份、`twitter:site`、
站长验证码）Site 不用来派生任何东西，模板里写一行即可（§6）。

总体设计 §4.1 列过的"标题模板""统计 ID"不做：`<title>` 和统计代码都由主题的 layout 自己写（统计代码写进 layout，用户 2026-11-05）。

### 4.2 `site_head` 的参数

现在是 `site_head [title] [description]`。新增一种写法，只传一个对象：

````
{{ site_head { image: content.fields.cover_image, type: "article", schema: [ ... ] } }}
````

| 键 | 说明 |
|---|---|
| `title` | 同现在：覆盖 `og:title`。 |
| `description` | 同现在：内容没有描述时用。 |
| `image` | 分享图的候选，内容的 SEO 字段没填分享图时用。可以是文件字段的值（取第一个文件），也可以是地址。 |
| `type` | `website`（默认）或 `article`。`article` 时写 `article:published_time`、`article:modified_time`。 |
| `schema` | 一个节点或节点数组，见 4.4。 |

对象里出现表外的键就渲染失败，同 `contents`。旧写法 `site_head "标题"` 照旧可用。

Razor 的 `<site-head-metadata>` 加同名属性 `image`、`og-type`、`schema`，和 `site_head` 共用 `SiteHeadMetadataHtml` 生成标签。

### 4.3 回退顺序与新增标签

**分享图**：SEO 字段的 `ogImage` → `image` 参数 → `Site.Seo.DefaultShareImage` → 不写。文件库里的图照旧裁成 1200×630，
并写 `og:image:width`、`og:image:height`；外部地址原样，不写宽高。有图时 `twitter:card` 是 `summary_large_image`，同现在。
`og:image:alt` 不写，由模板决定（用户 2026-11-05：不按地址查文件记录取文件名，多一次请求、价值不高）。

**新增标签**：

| 标签 | 值 |
|---|---|
| `og:site_name` | `Site.Branding.AppName`；为空不写。 |
| `og:locale` | 页面语言的地区形式：名字里带地区的照用（`en-GB` → `en_GB`）；中文按书写方式定（`zh-Hans` → `zh_CN`，`zh-Hant` → `zh_TW`）；其他语言用 `CultureInfo.CreateSpecificCulture` 推出（`ja` → `ja_JP`，`en` → `en_US`）。 |
| `og:locale:alternate` | hreflang 里的其他语言，同样换成地区形式。 |
| `og:type` | `type` 参数，默认 `website`。现在固定是 `website`。 |
| `article:published_time`、`article:modified_time` | `type: "article"` 时，内容的发布时间和最后修改时间，ISO 8601。 |

**不变**：`<title>` 仍由模板写（§5.3 的原则：模板知道品牌）；canonical、robots、hreflang、分页地址、描述的取法（只取 `metaDescription`）。

### 4.4 JSON-LD：模板给节点，Site 负责输出

`schema` 给一个对象或一个数组，Site 写一个 `<script type="application/ld+json">`：

- 多个节点合成 `{"@context": "https://schema.org", "@graph": [...]}`；只有一个节点时直接在它上面加 `@context`。
- 值为空的属性（null、空字符串、空数组、去空后变空的对象）去掉，任意深度。
- 字符串以 `~/` 开头的，按 `url` 函数补语言前缀，再补成主域名上的绝对地址；以 `/api/site-public/files/` 开头的文件地址也补成绝对地址。
  基准与 canonical 相同（`Site.PrimaryDomain`）。其他字符串原样。
- 用 System.Text.Json 的默认编码器输出，`<`、`>`、`&` 写成 `<` 这类转义，正文里的 `</script>` 不会提前结束脚本。
- 每个节点必须是对象并且有 `@type`，否则渲染失败。主题检查的试渲染能在发布前抓到。

Scriban 的对象字面量可以用带引号的键名，`@type`、`@id` 能直接写。主题指南（`site_get_theme_guide` 返回的那一页）给出常用写法：

````
{{ crumbs = [] }}
{{ for c in breadcrumb }}
  {{ crumbs = array.add crumbs { "@type": "ListItem", position: for.index + 1, name: c.name, item: c.url } }}
{{ end }}
{{ site_head { schema: [
  { "@type": "Organization", "@id": "~/#org", name: "Dignite", logo: "~/images/logo.png",
    sameAs: ["https://github.com/dignite-projects"] },
  { "@type": "BreadcrumbList", itemListElement: crumbs }
] } }}
````

### 4.5 面包屑数据

模板多一个变量 `breadcrumb`（Razor 是 `SiteRenderViewModel.Breadcrumb`），从首页到当前页的列表，每项有 `name`、`path`、`url`、`is_current`：

- 当前页面沿 `ParentId` 逐级向上到根，首页放在最前；当前就是首页时只有一项。
- 每项的名字按 head 取标题的同一规则，用当前语言：该页面自身内容的 SEO 标题 → 第一个有值的文本字段 → 页面的 `DisplayName`。
- 请求的是一条内容时，最后加上这条内容的标题。
- `path` 是带语言前缀的站内路径，用于页面上的链接；`url` 是主域名上的绝对地址，用于 JSON-LD。同 `HreflangAlternateDto` 的 `Path` 和 `Url`。
- 祖先页面的内容一次查回（按页面 ID 和语言），不按层数逐个查。

### 4.6 长度提示

SEO 字段的 `Seo.MetaTitleCharLimit`、`Seo.MetaDescriptionCharLimit` 改为按显示宽度计：中日韩全角字符算 2 列，其他算 1 列。
默认标题 60 列，描述 160 列；内容语言是中文或日文时描述按 200 列（沿用 cloud `SeoText` 的取值）。

仍然只是提示，不拦截保存：管理端 SEO 字段旁显示宽度计数；MCP 的 `site_create_content`、`site_update_content` 超出时在结果里带一条提醒。
渲染时不截短（§6）。键名不改，CHANGELOG 写明计法变了。

### 4.7 API 与 MCP

- `HeadMetadataDto`（`/api/site-public/head-metadata`）增加 `SiteName`、`Locale`、`LocaleAlternates`、`OgImageWidth`、`OgImageHeight`、
  `Breadcrumb`，默认分享图在这里就已经套上。`image`、`type`、`schema` 是模板的输入，不在 API 里，外部前端自己处理。
- 默认分享图在管理端"设置"页的 Site 选项卡里设置，MCP 加一个对应工具，名字实现时按现有工具命名定。
- `docs/en/seo/index.md`、`docs/en/rendering/scriban-templates.md`、`razor-templates.md` 按本文更新；`seo/index.md` 现在只写了 Razor
  的标签，顺带补上 `site_head`。

## 5. 兼容与版本

全是新增，没有数据库迁移：

- 旧的 `site_head` 写法和不加新属性的 `<site-head-metadata>`，输出只多出 `og:site_name`、`og:locale`、`og:locale:alternate`、
  设置了默认分享图时的默认图、文件库图片的宽高。
- 宿主如果自己写过这些标签（cloud 的 `Sites/Shared/_SiteLayout.cshtml` 就写了），升级后要删掉自己那份，否则重复。CHANGELOG 写明。
- 版本 2.2.0。

## 6. 不做

| 事项 | 原因 |
|---|---|
| 内容类型上的 schema 类型、字段映射；自动生成 Organization、WebSite、Article | #40 撤掉的正是这个，原则不变（§3） |
| 标题模板、自动拼站名 | `<title>` 留给模板（§5.3）。cloud "太长就去掉站名"在模板里自己判断 |
| 渲染时截短描述 | 截出的省略号是页面上没有的文字；搜索引擎本来就会改写摘要。改为编辑时提示（§4.6） |
| `twitter:site`、站长验证码、站点身份的设置 | Site 不用它们派生任何东西，模板里写一行即可 |
| `og:image:alt` | 由模板决定（用户 2026-11-05） |
| 文件记录加 alt、宽高字段 | 改动太大（用户 2026-11-05） |
| `rel=prev/next` | Google 2019 年起不再使用 |
| meta keywords | 搜索引擎不看 |
| 每条内容一段原始 JSON-LD（Orchard） | 编辑手写 JSON 容易错；`schema` 参数已覆盖 |
| 自动摘正文当描述（Ghost） | 2.0 刚去掉这类猜测 |

## 7. cloud 官网现有输出的去处

| 现在（cloud） | 迁到 Scriban 之后 |
|---|---|
| 无图页面用 `/images/share-default.jpg`，并写 1200×630 和 alt | `Site.Seo.DefaultShareImage`；alt 由 layout 自己写 |
| 博客封面当分享图 + 大图卡片 | `site_head { image: content.fields.cover_image, type: "article" }` |
| `og:site_name`、`og:locale`、`og:locale:alternate` | Site 自动写 |
| `<title>` 拼站名、太长就去掉站名（`SeoText.ComposeTitle`） | layout 自己写 `<title>` |
| 描述按宽度截短（`SeoText.Shorten`） | 不截短；超长的描述在内容里改短，编辑时有提示 |
| JSON-LD：Organization、WebSite | layout 里的 `schema` 节点（写死在主题里） |
| JSON-LD：BreadcrumbList | `breadcrumb` 变量 + `schema` 节点；面包屑不再由各模板手工设置。迁移时核对页面树和现在的面包屑是否一致 |
| JSON-LD：BlogPosting、Service、FAQPage | 各自的页面模板写 `schema` 节点 |
| JSON-LD：Campus 的 SoftwareApplication + AggregateOffer | 页面模板写 `schema` 节点；价格表从 `SiteContent/CampusFacts.cs` 挪进 pricing 页的 Matrix 字段，价格页和 JSON-LD 读同一份数据 |

## 8. 实现时的改动

- **长度提示只有一个上限。** 草稿写"内容语言是中文或日文时描述按 200 列"。SEO 字段在各语言之间共用一份配置，管理端的
  计数器也不知道内容的语言，按语言换上限只能在服务端做一半。改为只按宽度计（中日韩字符 2 列），上限仍是字段配置的
  `Seo.MetaTitleCharLimit` / `Seo.MetaDescriptionCharLimit`，默认 60 / 160；要放宽的站点改字段配置。
- **宽度计算放在 `Dignite.Site.Domain.Shared`**（`SeoTextWidth`）：MCP 只引用契约层，够不到 `Dignite.FlexFields.Site`。
  SEO 字段名和两个配置键在那里各写了一份，由测试断言与字段类型的常量相等。
- **面包屑逐级读祖先页面和它们自己的内容**，不是草稿说的"一次查回"：页面树通常只有两三层，按层查更简单。取标题要用的
  内容类型和字段库对所有步骤只读一次（发版前审查发现原先每一级都重读整个字段库）。页面自身的内容（空 slug）算作页面
  那一步，不再多出一步。
- **`og:locale` 不全交给运行时**：发版前审查发现 Windows 上 `CreateSpecificCulture("zh-Hant")` 给的是 `zh-HK`，
  不同平台的 ICU 也可能不一样。改为名字带地区的照用、中文按书写方式定（简体 `zh_CN`、繁体 `zh_TW`），其余语言才问运行时。
- **默认分享图的入口**是 Setting Management 上新增的 **SEO** 选项卡（和语言选项卡同一个入口点），MCP 工具叫
  `site_set_default_share_image`，权限 `SiteAdmin.Seo` / `SiteAdmin.Seo.Update`。
- **`og:image:alt` 不写**（用户 2026-11-05 先说取文件名，又改为交给模板）。
- **JSON-LD 的 `~/`** 按 `url` 函数的规则加语言前缀：日文页上的 `~/` 是 `/ja`，所以 `~/#org` 写成 `https://.../ja#org`。
  要跨语言共用一个 `@id`，写绝对地址。
