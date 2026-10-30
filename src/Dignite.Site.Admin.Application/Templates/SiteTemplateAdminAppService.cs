using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace Dignite.Site.Admin.Templates;

[Authorize(SiteAdminPermissions.Templates.Default)]
public class SiteTemplateAdminAppService : SiteAdminAppService, ISiteTemplateAdminAppService
{
    protected ISiteTemplateRepository TemplateRepository { get; }

    protected SiteTemplateManager TemplateManager { get; }

    public SiteTemplateAdminAppService(
        ISiteTemplateRepository templateRepository,
        SiteTemplateManager templateManager)
    {
        TemplateRepository = templateRepository;
        TemplateManager = templateManager;
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
        var templates = await TemplateRepository.GetListAsync(input.Filter, includeDetails: false);

        var latestVersionNumbers = await TemplateRepository.GetLatestVersionNumbersAsync(templates.Select(t => t.Id));

        var items = templates
            .Select(template => MapToDto(template, latestVersionNumbers.GetValueOrDefault(template.Id)))
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

    protected virtual SiteTemplateDto MapToDto(SiteTemplate template, int latestVersionNumber)
    {
        var dto = ObjectMapper.Map<SiteTemplate, SiteTemplateDto>(template);
        dto.LatestVersionNumber = latestVersionNumber;
        return dto;
    }
}
