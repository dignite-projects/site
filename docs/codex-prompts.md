# Codex 任务提示词

按 GitHub issue 的 `group:` 标签组织。每个任务都要**先粘共用前言**，再粘具体任务段。

| 分组 | 标签 | 提示词 |
|---|---|---|
| 接口层 | `group: 1-interface` | [I1](#i1--4-application-层) [I2](#i2--5-httpapi-层) |
| 站点地基 | `group: 2-foundation` | [F1](#f1--11-12-19-站点地基) |
| 可发现性 SEO | `group: 3-seo` | [S1](#s1--14-seo-字段组预设) [S2](#s2--15-18-31-独立端点) [S3](#s3--13-16-17-20-页内-head-输出) |
| MCP | `group: 5-mcp` | [M1](#m1--25-工具面设计决策) [M2](#m2--26-mcp-server-实现) [M3](#m3--27-ai-生成-seo-字段) |
| 渲染前端 | `group: 4-rendering` | 待 #28 / #22 决策后再写 |
| 内核欠账 | `group: 0-kernel-debt` | 9 条都小而独立，直接 `gh issue view N` 即可 |

---

## 共用前言

```text
You are working on Dignite.Site, an ABP Framework 10.5 / .NET 10 multi-tenant site
platform at D:\dignite-projects\sites.

READ FIRST
- docs/00-总体设计.md is the authoritative design document (Chinese). Read the sections
  the task references before writing code. It records what was rejected and why.
- The P0 content kernel is DONE: Page / ContentType / Content / Field entities, the
  FlexFields wiring, multi-tenancy, multi-language, and route resolution. 97 tests pass.
  Do not redesign it; build on it.

HARD CONSTRAINTS — violating these breaks things silently, not loudly
1. D:\dignite-projects\abp-modules\flex-fields is a SEPARATE repository, referenced by
   ProjectReference for local iteration. Do NOT modify it. Other downstreams consume it.
2. Always write through the domain managers, never straight to repositories:
   - ContentManager  — holds slug uniqueness, culture normalization, page/content-type
     consistency, field validation, and query-index synchronization.
   - FieldManager    — holds the field rename/delete ordering. A field's Name IS the key
     its values are stored under in every content's value bag, so renaming it is a data
     migration. NEVER call Field.SetName directly; it orphans every stored value.
   - ContentTypeManager — reindexes when a Searchable flag flips.
3. Culture names go through CultureNameNormalizer. Its `predefinedOnly: true` is
   load-bearing: without it .NET invents a culture from any string prefix that parses
   ("not-a-culture-at-all" becomes "not"), which splits a content's translation group.
4. An EMPTY slug is meaningful — it is the single content of a page (a home or "about"
   page), whose URL is the page route itself. Never treat empty as "missing".
5. The flex-field query index is DERIVED. The authoritative value is always in
   Content.FlexFields. Index writes must ride the same unit of work as the save.
6. Column lengths for Field's own columns come from FlexFieldConsts via
   ConfigureFlexField<Field>(). Do not restate them.

VERIFY
  dotnet build Dignite.Site.slnx
  dotnet test  Dignite.Site.slnx      # 97 tests must stay green
Add tests for what you write. Integration tests go in
test/Dignite.Site.EntityFrameworkCore.Tests (in-memory SQLite, seeded blog scenario in
SiteTestDataSeedContributor); pure unit tests in test/Dignite.Site.Domain.Tests.
Do not commit unless asked.
```

---

## I1 — #4 Application 层

```text
TASK: GitHub issue #4 — Application and Application.Contracts layer for the content kernel.
    gh issue view 4 --repo dignite-projects/sites

The Domain and EntityFrameworkCore layers are complete. The *.Application and
*.Application.Contracts projects are still template scaffold — the kernel currently has no
caller except its own tests.

Build DTOs, application services, permissions and object mapping (the solution uses
Mapperly) for Page, ContentType, Field, FieldGroup and Content.

Design notes specific to this task:
- Keep the app layer THIN. Every invariant already lives in a domain manager. If you find
  yourself re-checking slug uniqueness or re-validating field values, you are duplicating
  ContentManager and the two copies will drift.
- A content's field values cross the boundary as a loose dictionary. ContentManager already
  drops keys the content type does not declare and already runs each value through its
  field type's validation. Do not pre-filter or pre-validate in the DTO layer.
- Design the flex-field value DTO so FlexFieldDictionary does not leak into the contract,
  but round-trips faithfully. Values come back from JSON storage as JsonElement — the
  kernel's GetField<T>() extension already handles that unwrapping.
- The scaffold has Admin / Public / Common / unified project variants. Decide deliberately
  which services belong in which; the Public one is read-only and serves published content
  only.

Report at the end which decisions you made about the split and the value DTO shape.
```

## I2 — #5 HttpApi 层

```text
TASK: GitHub issue #5 — HttpApi layer, including the resolve-path endpoint.
    gh issue view 5 --repo dignite-projects/sites

Depends on #4 being done.

Expose the application services over REST, plus the resolve-path contract from design doc
§7.4:  resolve-path(path) -> { matched, page, content, contentType }
SiteRouteResolver already implements the resolution and returns a RouteMatch; this is its
HTTP projection, and it is what an out-of-process (Tier 1) front end consumes.

TRAP — read before naming any method:
ABP's conventional controllers derive the URL from the METHOD NAME, not from an attribute
route template. A method named GetListAsync collides with CreateAsync on the same URL no
matter what [HttpPost("...")] you put on it. Name search-style POST endpoints SearchAsync,
not GetListAsync.

Also:
- RouteMatch.Kind distinguishes Page / ContentOfPage / Content. Preserve that distinction
  in the response; the back end deliberately does not decide whether a page renders as a
  list or a single page, and a front end needs the raw signal.
- Preview access (includeUnpublished: true) must be permission-gated, and its response has
  to be marked noindex by whatever renders it. Document that on the endpoint.
```

## F1 — #11 #12 #19 站点地基

```text
TASK: GitHub issues #11, #12 and #19 — the site foundation. Do them together; they are one
coherent piece of work.
    gh issue view 11 --repo dignite-projects/sites
    gh issue view 12 --repo dignite-projects/sites
    gh issue view 19 --repo dignite-projects/sites

#11 Tenant-level site settings — design doc §4.1. The Site aggregate was deliberately
REMOVED: languages, default language, theme, analytics id, robots policy, default OG image
and title template are all settings, and ABP's setting system already gives per-tenant
isolation. Define them in the existing (empty) SiteSettingDefinitionProvider under
SiteSettings.GroupName. Decide which are host-only and which tenants may override.

#12 Domain-Names integration — design doc §4.2. Domains are the ONE exception to #11,
because the question is reversed: not "what is configured for this tenant" but "given this
Host header, which tenant is it". That is a lookup, and it lands on the existing Dignite
Domain-Names module. One primary domain per site; secondary domains 301 to it.
Why the 301 matters: a custom domain and the platform subdomain both serving 200 is
duplicate content, the classic multi-tenant SEO failure, and it splits ranking signals.

#19 Redirect table + real 404 — design doc §3.4 step 3 and §5.7. When SiteRouteResolver
misses, the next lookup is a per-tenant redirect table, and only then a 404.
- Auto-create a 301 when a content's slug changes. ContentManager.UpdateAsync already
  detects the slug change and re-checks uniqueness — that is the hook.
- Flatten chains: if A->B exists and B->C is added, rewrite A->C.
- The 404 must return status 404. A 200 with a "not found" template is a soft 404 and gets
  the page indexed as real content.

Sequencing: #11 and #12 first (they have no dependency on the Application layer), #19 last.
```

---

## S1 — #14 SEO 字段组预设

先做这条。#13 和 #15 都认它。

```text
TASK: GitHub issue #14 — Standard SEO field group preset.
    gh issue view 14 --repo dignite-projects/sites

Design doc §5.3. The platform pre-seeds a standard set of SEO field DEFINITIONS into every
tenant's field library. A content type opts in by pulling them into its field arrangement,
and pulling them in is what gives that type platform-level SEO behaviour. A type that does
not pull them in falls back to platform defaults.

This is a content-kernel task, not an SEO-rendering task. It produces field definitions and
one recognized semantic; nothing here emits a tag.

WHY A PRESET RATHER THAN PER-TYPE FIELDS
The definitions live in one place and are referenced BY ID. That is what lets the platform
recognize `noindex` without guessing at field names per content type. Design doc §5.3 calls
this out explicitly as the payoff of a separate field-definition table.

SCOPE
- Seed the definitions with STABLE well-known Guids (meta title, meta description,
  og:image, noindex, ...). Pick the set from §5.3 and §5.9; justify anything you add.
- Seed per tenant. Study how the existing seeding works before inventing a mechanism.
- Define the `noindex` semantic: a content whose noindex field is truthy must be excluded
  from the sitemap (#15 consumes this) and must emit a noindex meta tag (#13 consumes it).
  Put the recognition logic in ONE place both can call — not duplicated in each.
- Field types come from the FlexFields kernel's six built-ins. The registration keys are
  "TextEdit", "NumericEdit", "DateEdit", "Select", "Switch", "TreeView". noindex is a
  Switch. Do not invent new field types for this.

THE QUESTION WORTH ANSWERING BEFORE CODING
The platform recognizes these fields by id, so a tenant who deletes or renames a preset
field silently disables the semantic — the sitemap would stop honouring noindex with no
error anywhere. Decide what to do: protect presets from deletion, detect-and-reseed, or
accept it and degrade loudly. Write down which you chose and why.

Note FieldManager.DeleteAsync strips a field's values out of every content's bag, so a
deleted preset is not merely unhooked — the stored values go too.
```

## S2 — #15 #18 #31 独立端点

三条都是"按域名生成一个文件"，形状一样，一起做。依赖 #12（域名→租户）。

```text
TASK: GitHub issues #15, #18 and #31 — the standalone per-domain endpoints. Do them
together; they share a shape (resolve tenant from host, render a document) and a set of
traps.
    gh issue view 15 --repo dignite-projects/sites
    gh issue view 18 --repo dignite-projects/sites
    gh issue view 31 --repo dignite-projects/sites

Depends on #12 (host -> tenant resolution) for absolute URLs and per-domain isolation.
#15 also depends on #14 for the noindex semantic.

#15 XML SITEMAP — design doc §5.2
Generated from the Page and Content tables: every base page route, plus every published
content expanded through its page's content path pattern. Page.BuildContentPath() already
produces those URLs — use it, do not re-derive URL composition.
- Adopt X.Web.Sitemap (MIT) for sharding and index generation. Write only the glue that
  feeds it URLs, shard keys and lastmod.
- One sitemap index per tenant, namespaced so tenants can never see each other's.
- Shard at 10-20k URLs per file with gzip (the hard ceiling is 50k / 50MB).
- Exclude drafts, archived contents, and anything #14's noindex semantic marks.

  *** THE lastmod FOOTGUN — read before writing a single line of it ***
  lastmod must move ONLY when the content genuinely changed. If every URL reports today's
  date, Google ignores the field across the ENTIRE sitemap — you lose it for the whole
  site, not just the wrong rows. Drive it from the content's real modification time or a
  content hash. Never from generation time. See also #6: a scheduled publish that changed
  nothing else should not move it either.

#18 ROBOTS.TXT — design doc §5.6
Per domain, resolved through the host->tenant lookup. Strict namespace isolation: one
tenant's rules must never appear on another's domain. Reference that tenant's sitemap index.
- AI crawler toggles are the differentiating part and should be first-class per-tenant
  settings (#11), not free-text robots editing. Distinguish the two classes, because owners
  routinely want opposite answers for them:
    TRAINING:     GPTBot, Google-Extended, ClaudeBot, CCBot
    SEARCH / RAG: OAI-SearchBot, PerplexityBot, Claude-SearchBot
  The common policy is allow search / block training. Make that the easy choice.
- llms.txt: generate it if cheap, but mark it optional and do not present it as a ranking
  mechanism — roughly 10% adoption and Google has said it does not support it.

#31 FEEDS — design doc §5.9
RSS/Atom via System.ServiceModel.Syndication (MIT), JSON Feed via JsonFeed.NET (MIT).
Same data source as the sitemap: published contents under a page. Per page and per
language. Advertise with <link rel="alternate"> for autodiscovery (that link tag is
emitted by the head-output work in #13/#16/#17/#20 — coordinate, do not duplicate).

SHARED
All three need absolute URLs on the tenant's PRIMARY domain, never on whatever host the
request happened to arrive on. Build one helper for that and use it in all three; it is the
same helper #16 (canonical) needs.
```

## S3 — #13 #16 #17 #20 页内 head 输出

四条都往 `<head>` 里写东西，抽象层一致。

```text
TASK: GitHub issues #13, #16, #17 and #20 — everything emitted into the page <head>.
    gh issue view 13 --repo dignite-projects/sites
    gh issue view 16 --repo dignite-projects/sites
    gh issue view 17 --repo dignite-projects/sites
    gh issue view 20 --repo dignite-projects/sites

Depends on #14 (SEO field preset) and #12 (primary domain).

Build these as SERVICES that produce head metadata for a resolved RouteMatch, so the Tier 0
renderer (#21) and any future renderer call the same thing. Do not couple them to a view.

#13 META / OPEN GRAPH / TWITTER — design doc §5.3, §5.9
Emit through Dignite.Abp.Seo (SeoTags), which already covers these. This should be close to
zero new tag-building code — feed it, do not reimplement it.
Values come from the SEO field group the content type pulled in (#14), stored per language
like any other field. Fall back to platform defaults when a type did not pull them in, so
an unconfigured site is still acceptable out of the box (§5.1 principle 1).
og:image at 1200x630, under 1MB, subject in the centred safe area. Twitter
summary_large_image.

#16 CANONICAL — design doc §5.3(2)
DERIVED, not authored: computed from the primary domain and the resolved route. It does not
read any SEO field. Self-referencing, absolute, on the primary domain from #12.
  *** Preview responses (SiteRouteResolver.ResolveAsync with includeUnpublished: true) MUST
  be forced to noindex. An indexable preview is duplicate content against the real URL. The
  resolver's own XML docs say the caller has to do this — you are that caller. ***

#17 HREFLANG — design doc §5.5
The data model makes this nearly free. A content's language versions are the rows sharing
the natural key (PageId, ContentTypeId, Slug) and differing in CultureName. There is NO
translation-group table and no group id that can drift.
IContentRepository.GetTranslationsAsync already returns exactly that set — use it.
- CultureName is stored in canonical CultureInfo.Name BCP 47 form specifically so it can be
  used as the hreflang value VERBATIM. Do not build a mapping table.
- Reciprocal, self-referencing, absolute URLs.
- x-default points at the home page (PageRoute.IsHomeRoute - a page's own address landing on
  the site root, not a stored flag. FindHomePageAsync resolves ties the same way
  SiteRouteResolver does for any other shared address, so there's always at most one).
- Default URL strategy is the subdirectory form (/en/, /zh/). Slugs are identical across
  languages by design (§2.4), so alternates differ only by prefix.

#20 JSON-LD — design doc §5.4, via Schema.NET (MIT)
Only the types that still produce rich results: Organization and WebSite (site-wide),
BreadcrumbList (from the route structure), Article / NewsArticle / Product+Offer.
A `schemaOrg` mapping on the content type plus a field mapping drives it.
  *** The 2025/2026 landscape shrank — do not scope from older guidance: ***
  - FAQPage and HowTo rich results are GONE (FAQ retired May 2025, HowTo dead since 2023).
    Emitting them for AI consumption is defensible; expecting a SERP treatment is not.
  - Product images require 500x500 minimum — warning since April 2026, enforced Jan 2027.
Validate against the Rich Results test as a pre-publish gate.
```

---

## M1 — #25 工具面设计（决策）

这条**不是写代码**，产出是一份决策，写回 issue。

```text
TASK: GitHub issue #25 — Design the MCP tool surface. This is a DESIGN task. The
deliverable is a written decision posted as a comment on issue #25 (and a short section in
docs/), NOT an implementation.
    gh issue view 25 --repo dignite-projects/sites

Read design doc §1.2, §6 and §10.2 first. §6.2 deliberately deferred this until the
structure was stable. It now is (P0 landed), so this is unblocked.

THE QUESTION
How are the tools cut?
  - BY ENTITY (create_page, create_content_type, create_content, ...) — maps directly onto
    the domain, but forces the client through multi-step sequences for what a user states
    as a single intent.
  - BY SCENARIO (publish_article, create_blog) — matches how a user actually speaks ("post
    a news item, here is the body"), but the platform has to infer more.
  - READ/WRITE SPLIT, for permissioning and read-only clients.

Also decide: how tool permissions map onto ABP's existing permission system, and whether
MCP access uses the same tokens as the HTTP API.

WHAT THE TOOLS MUST EXPOSE
Design doc §1.2 principle 3 — "schema is the contract". A content type's field arrangement
is simultaneously the write-validation gate AND the target shape handed to the AI.
ContentType.Description and Field.Description are written for that audience specifically.
IFlexFieldProvider<Content> already resolves the full picture for a given content — study
it before designing anything that re-derives the same thing.

CONSTRAINT THAT SHAPES THE ANSWER
MCP is a SECOND WRITE PATH into the same domain, which is where duplicated validation
usually creeps in. ContentManager holds slug uniqueness, culture normalization,
page/content-type consistency and field validation; FieldManager holds the rename ordering
that keeps stored values reachable. Tools are another caller, not another implementation.
Whatever cut you choose has to be expressible on top of the existing managers without
reaching around them.

Evaluate the three cuts against: how many round trips a realistic "post a news item"
takes; how well each maps to ABP permissions; and how much the platform has to guess.
Recommend one, and say what you are giving up.
```

## M2 — #26 MCP server 实现

```text
TASK: GitHub issue #26 — Implement the MCP server.
    gh issue view 26 --repo dignite-projects/sites

Depends on #25 (the tool-surface decision) and #4 (Application layer).

Design doc §6.1: the platform does NOT embed an AI assistant. The differentiator is that
platform capability is exposed AS MCP, with the intelligence living in the user's own AI
client. Both site building and day-to-day operation go through that one path:
  - Building: describe a page and its route, define content types under it and pull fields
    in (creating field definitions as needed), fill in content.
  - Operating: "post a news item, here is the body" — the client calls the tool, the
    platform validates against the field definitions and stores it, and routes and sitemap
    update themselves.

SCOPE
- MCP server endpoint and transport.
- Authentication and per-tenant scoping. Every tool call must run inside the right tenant;
  ABP's data filter does the rest.
- Tool implementations over the application services from #4.

TRAPS
- Culture: every write path normalizes through CultureNameNormalizer. An AI client will
  happily send "zh-cn" or "Chinese". Reject or normalize — never store as given.
- Slug: an AI client will often omit it. Empty slug is MEANINGFUL (the single content of a
  page), so "omitted" and "deliberately empty" are different. SlugNormalizer.TryNormalize
  exists precisely to distinguish "nothing survived normalization" from "the caller meant
  empty" — use it rather than Normalize.
- Errors must be machine-readable. A tool call that fails validation should tell the client
  WHICH field and WHY, so the AI can correct itself and retry. The kernel's validator
  already returns per-field ValidationResults with the field name attached; ContentManager
  wraps them in an AbpValidationException. Surface that structure, do not flatten it to a
  string.
```

## M3 — #27 AI 生成 SEO 字段

```text
TASK: GitHub issue #27 — AI-assisted SEO field generation.
    gh issue view 27 --repo dignite-projects/sites

Depends on #25/#26 (tool surface) and #14 (SEO field preset).

Design doc §5.1 principle 4: the AI client is already writing the content, so it should
produce the metadata in the same pass — meta description, image alt text, structured data,
and the direct-answer opening paragraph.

This is LESS A FEATURE THAN A SHAPE. If the SEO fields are part of the content type's
schema handed to the client, filling them is the natural thing to do rather than an extra
step, and they go through the same validation gate as every other field. So most of the
work here is making sure the tool surface presents them well — descriptions written for an
AI audience, sensible required/optional flags — rather than building a generation pipeline.

Do NOT build a server-side LLM call. The platform deliberately does not embed an assistant
(§1.2 principle 2); the intelligence is in the user's client.

ACCESSIBILITY OVERLAP (§5.9)
AI-generated alt text is also the WCAG 2.2 AA item most likely to be skipped by a human
author. Treat it as part of this rather than as a separate accessibility task.
```
