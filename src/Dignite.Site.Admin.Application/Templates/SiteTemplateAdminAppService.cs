using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Contents;
using Dignite.Site.Pages;
using Dignite.Site.Seo;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Threading;

namespace Dignite.Site.Admin.Templates;

[Authorize(SiteAdminPermissions.Templates.Default)]
public class SiteTemplateAdminAppService : SiteAdminAppService, ISiteTemplateAdminAppService
{
    /// <summary>
    /// How many of a page's newest public contents are looked at for one with a slug in a served language.
    /// The newest one nearly always is; the few after it cover a page whose own empty-slug content, or a
    /// content in a language the site no longer serves, happens to be the newest.
    /// </summary>
    protected const int SampleContentCandidateCount = 10;

    protected ISiteTemplateRepository TemplateRepository { get; }

    protected SiteTemplateManager TemplateManager { get; }

    protected SiteTemplateUsageCalculator UsageCalculator { get; }

    protected IContentRepository ContentRepository { get; }

    protected SiteLanguageProvider LanguageProvider { get; }

    protected ISiteBaseUrlResolver BaseUrlResolver { get; }

    public SiteTemplateAdminAppService(
        ISiteTemplateRepository templateRepository,
        SiteTemplateManager templateManager,
        SiteTemplateUsageCalculator usageCalculator,
        IContentRepository contentRepository,
        SiteLanguageProvider languageProvider,
        ISiteBaseUrlResolver baseUrlResolver)
    {
        TemplateRepository = templateRepository;
        TemplateManager = templateManager;
        UsageCalculator = usageCalculator;
        ContentRepository = contentRepository;
        LanguageProvider = languageProvider;
        BaseUrlResolver = baseUrlResolver;
    }

    public virtual async Task<SiteTemplateDto> GetAsync(Guid id)
    {
        var template = await TemplateRepository.GetAsync(id, includeDetails: true);
        return MapToDto(template);
    }

    public virtual async Task<SiteTemplateDto?> FindByNameAsync(string name)
    {
        var template = await TemplateRepository.FindByNameAsync(SiteTemplate.NormalizeName(name), includeDetails: true);
        return template == null ? null : MapToDto(template);
    }

    public virtual async Task<ListResultDto<SiteTemplateDto>> GetListAsync(GetSiteTemplateListInput input)
    {
        // Without details: a list must not drag every version of every template out of the database. The
        // newest number of each comes back in one grouped query instead.
        var templates = await TemplateRepository.GetListAsync(input.Filter, input.Prefix, includeDetails: false);

        var latestVersionNumbers = await TemplateRepository.GetLatestVersionNumbersAsync(templates.Select(t => t.Id));

        // Over every template, not just the filtered ones: a partial is used by templates the filter may hide.
        var usage = await UsageCalculator.CalculateAsync(GetCancellationToken());

        var items = templates
            .Select(template =>
            {
                var dto = MapToDto(template, latestVersionNumbers.GetValueOrDefault(template.Id));
                dto.UsageCount = usage.GetUsageCount(template.Name);
                return dto;
            })
            .ToList();

        return new ListResultDto<SiteTemplateDto>(items);
    }

    [Authorize(SiteAdminPermissions.Templates.Create)]
    public virtual async Task<SiteTemplateDto> CreateAsync(CreateSiteTemplateDto input)
    {
        var template = await TemplateManager.CreateAsync(
            name: input.Name,
            body: input.Body,
            comment: input.Comment,
            publish: input.Publish);

        return MapToDto(template);
    }

    [Authorize(SiteAdminPermissions.Templates.Update)]
    public virtual async Task<SiteTemplateDto> UpdateAsync(Guid id, UpdateSiteTemplateDto input)
    {
        var template = await TemplateRepository.GetAsync(id, includeDetails: true);

        await TemplateManager.AddVersionAsync(template, input.Body, input.Comment, input.Publish);

        return MapToDto(template);
    }

    [Authorize(SiteAdminPermissions.Templates.Update)]
    public virtual async Task<SiteTemplateDto> PublishAsync(Guid id, int versionNumber)
    {
        var template = await TemplateRepository.GetAsync(id, includeDetails: true);

        await TemplateManager.PublishAsync(template, versionNumber);

        return MapToDto(template);
    }

    public virtual async Task<ListResultDto<SiteTemplateVersionDto>> GetVersionsAsync(Guid id)
    {
        var template = await TemplateRepository.GetAsync(id, includeDetails: true);

        var items = template.Versions
            .OrderByDescending(version => version.Number)
            .Select(version =>
            {
                var dto = ObjectMapper.Map<SiteTemplateVersion, SiteTemplateVersionDto>(version);
                // A history of N versions would otherwise carry N full sources; the body of one version is
                // what GetVersionAsync is for.
                dto.Body = string.Empty;
                return dto;
            })
            .ToList();

        return new ListResultDto<SiteTemplateVersionDto>(items);
    }

    public virtual async Task<SiteTemplateVersionDto> GetVersionAsync(Guid id, int versionNumber)
    {
        var template = await TemplateRepository.GetAsync(id, includeDetails: true);

        var version = template.FindVersion(versionNumber)
                      ?? throw new EntityNotFoundException(typeof(SiteTemplateVersion), versionNumber);

        return ObjectMapper.Map<SiteTemplateVersion, SiteTemplateVersionDto>(version);
    }

    public virtual async Task<SiteTemplatePreviewDto> PreviewAsync(Guid id, int versionNumber, string path)
    {
        var template = await TemplateRepository.GetAsync(id, includeDetails: true);

        var version = template.FindVersion(versionNumber)
                      ?? throw new EntityNotFoundException(typeof(SiteTemplateVersion), versionNumber);

        // Resolved lazily and optionally: the renderer is Dignite.Site.Public.Web's, which this layer cannot
        // reference, and an administration API hosted without it simply cannot preview.
        var renderer = LazyServiceProvider.LazyGetService<ISiteTemplatePreviewRenderer>()
                       ?? throw new BusinessException(SiteErrorCodes.SiteTemplatePreviewUnavailable);

        return await renderer.RenderAsync(new SiteTemplatePreviewRequest
        {
            TemplateName = template.Name,
            Version = ObjectMapper.Map<SiteTemplateVersion, SiteTemplateVersionDto>(version),
            Path = path
        }, GetCancellationToken());
    }

    public virtual async Task<SiteTemplateUsageDto> GetUsageAsync(Guid id)
    {
        // Without details: only the name is needed, the versions the graph reads come back one per template.
        var template = await TemplateRepository.GetAsync(id, includeDetails: false);
        var cancellationToken = GetCancellationToken();

        var usage = await UsageCalculator.CalculateAsync(cancellationToken);
        // Built the way SiteUrlBuilder builds its context's languages: the first enabled one is the default.
        var cultureNames = await LanguageProvider.GetEnabledLanguagesAsync(cancellationToken);
        var languages = new SiteLanguages(cultureNames[0], cultureNames);

        var pages = new List<SiteTemplatePageUsageDto>();
        foreach (var page in usage.GetPagesRendering(template.Name))
        {
            pages.Add(await MapToPageUsageDtoAsync(page, languages, cancellationToken));
        }

        return new SiteTemplateUsageDto
        {
            TemplateId = template.Id,
            TemplateName = template.Name,
            Kind = SiteTemplateNames.GetKind(template.Name),
            Pages = pages,
            ReferencedBy = usage.GetReferencesTo(template.Name).Select(MapToReferenceDto).ToList()
        };
    }

    public virtual async Task<ListResultDto<SiteTemplateUnmatchedPageDto>> GetUnmatchedPagesAsync()
    {
        var usage = await UsageCalculator.CalculateAsync(GetCancellationToken());

        var items = new List<SiteTemplateUnmatchedPageDto>();

        foreach (var page in usage.Pages)
        {
            var templateName = SiteTemplateNames.Normalize(page.Template);
            var current = usage.FindCurrentVersion(templateName);

            if (current?.PublishedVersionNumber != null)
            {
                continue;
            }

            items.Add(new SiteTemplateUnmatchedPageDto
            {
                PageId = page.Id,
                Name = page.Name,
                DisplayName = page.DisplayName,
                Template = page.Template,
                TemplateId = current?.TemplateId,
                HasDraft = current != null
            });
        }

        return new ListResultDto<SiteTemplateUnmatchedPageDto>(items);
    }

    public virtual async Task<SiteTemplatePreviewLinkDto> GetPreviewLinkAsync(Guid id, int versionNumber, string path)
    {
        var template = await TemplateRepository.GetAsync(id, includeDetails: true);

        var version = template.FindVersion(versionNumber)
                      ?? throw new EntityNotFoundException(typeof(SiteTemplateVersion), versionNumber);

        // Resolved lazily and optionally, like the preview renderer: the issuer is Dignite.Site.Public.Web's -
        // it signs with the keys the site verifies with - and an administration API hosted without it has none.
        var issuer = LazyServiceProvider.LazyGetService<ISiteTemplatePreviewLinkIssuer>()
                     ?? throw new BusinessException(SiteErrorCodes.SiteTemplatePreviewLinkUnavailable);

        var cancellationToken = GetCancellationToken();

        return await issuer.IssueAsync(new SiteTemplatePreviewLinkRequest
        {
            TemplateName = template.Name,
            VersionNumber = version.Number,
            Path = path,
            // The tenant's primary domain when one is configured; null lets the issuer fall back to the request,
            // which is the right origin when the administration side and the site share one host.
            BaseUrl = await BaseUrlResolver.GetBaseUrlAsync(cancellationToken)
        }, cancellationToken);
    }

    [Authorize(SiteAdminPermissions.Templates.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var template = await TemplateRepository.GetAsync(id, includeDetails: true);

        await TemplateManager.DeleteAsync(template);
    }

    /// <summary>
    /// Maps a template fetched with <c>includeDetails: true</c>, so <see cref="SiteTemplate.Versions"/> is
    /// already loaded and the newest number costs no extra query.
    /// </summary>
    protected virtual SiteTemplateDto MapToDto(SiteTemplate template)
    {
        return MapToDto(template, template.GetLatestVersion()?.Number ?? 0);
    }

    /// <summary>
    /// <see cref="SiteTemplateDto.Kind"/> is filled on every read - it is the name's, and costs nothing;
    /// <see cref="SiteTemplateDto.UsageCount"/> is left null here and filled by the list only, which works the
    /// usage out once for all rows.
    /// </summary>
    protected virtual SiteTemplateDto MapToDto(SiteTemplate template, int latestVersionNumber)
    {
        var dto = ObjectMapper.Map<SiteTemplate, SiteTemplateDto>(template);
        dto.LatestVersionNumber = latestVersionNumber;
        dto.Kind = SiteTemplateNames.GetKind(template.Name);
        return dto;
    }

    protected virtual async Task<SiteTemplatePageUsageDto> MapToPageUsageDtoAsync(
        Page page, SiteLanguages languages, CancellationToken cancellationToken)
    {
        var hasSlug = PageRoute.HasSlug(page.Route);

        return new SiteTemplatePageUsageDto
        {
            PageId = page.Id,
            Name = page.Name,
            DisplayName = page.DisplayName,
            Route = page.Route,
            Template = page.Template,
            IsHomePage = page.IsHomeRoute(),
            HasSlug = hasSlug,
            Path = page.GetPath(),
            SampleContent = hasSlug ? await FindSampleContentAsync(page, languages, cancellationToken) : null
        };
    }

    /// <summary>
    /// The newest publicly visible content of <paramref name="page"/> that has an address of its own: a slug -
    /// the empty-slug content is the page's own address, which <see cref="SiteTemplatePageUsageDto.Path"/>
    /// already is - in a language the site serves, since a content in any other language has a URL the site
    /// answers with 404. One query per page with a slug; a site has few pages.
    /// </summary>
    protected virtual async Task<SiteTemplateSampleContentDto?> FindSampleContentAsync(
        Page page, SiteLanguages languages, CancellationToken cancellationToken)
    {
        var candidates = await ContentRepository.GetListAsync(
            pageId: page.Id,
            status: ContentStatus.Published,
            publishedBefore: Clock.Now,
            maxResultCount: SampleContentCandidateCount,
            sorting: $"{nameof(Content.PublishTime)} desc, {nameof(Content.Id)} desc",
            cancellationToken: cancellationToken);

        var content = candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c.Slug) && languages.IsServed(c.CultureName));

        if (content == null)
        {
            return null;
        }

        return new SiteTemplateSampleContentDto
        {
            ContentId = content.Id,
            Slug = content.Slug,
            CultureName = content.CultureName,
            // The same composition SiteUrlBuilder.BuildContentPath does, without needing a base URL: a
            // relative path does not depend on the domain, and the preview must work before one is configured.
            Path = languages.ApplyCulturePrefix(page.BuildContentPath(content), content.CultureName)
        };
    }

    protected virtual SiteTemplateReferenceDto MapToReferenceDto(SiteTemplateUsageReference reference)
    {
        return new SiteTemplateReferenceDto
        {
            TemplateId = reference.Source.TemplateId,
            TemplateName = reference.Source.TemplateName,
            Kind = SiteTemplateNames.GetKind(reference.Source.TemplateName),
            VersionNumber = reference.Source.Number,
            IsPublished = reference.Source.IsPublished,
            FunctionName = reference.FunctionName
        };
    }

    protected virtual CancellationToken GetCancellationToken()
    {
        return LazyServiceProvider.LazyGetService<ICancellationTokenProvider>()?.Token ?? CancellationToken.None;
    }
}
