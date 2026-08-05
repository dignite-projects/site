using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Common;
using Dignite.Site.Contents;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Timing;

namespace Dignite.Site.Public.Contents;

/// <summary>Published content only, enforced on every path - by id, by slug, in lists, and in translations.</summary>
public class ContentPublicAppService : PublicAppService, IContentPublicAppService
{
    protected IContentRepository ContentRepository { get; }

    protected IClock Clock { get; }

    public ContentPublicAppService(IContentRepository contentRepository, IClock clock)
    {
        ContentRepository = contentRepository;
        Clock = clock;
    }

    public virtual async Task<ContentDto> GetAsync(Guid id)
    {
        var content = await ContentRepository.GetAsync(id);
        EnsurePublished(content);
        return MapToDto(content);
    }

    public virtual async Task<ContentDto> GetBySlugAsync(Guid pageId, string cultureName, string slug)
    {
        var content = await ContentRepository.FindBySlugAsync(pageId, cultureName, slug);

        if (content == null || !content.IsPublished(Clock.Now))
        {
            throw new EntityNotFoundException(typeof(Content));
        }

        return MapToDto(content);
    }

    public virtual async Task<PagedResultDto<ContentDto>> GetListAsync(GetPublicContentListInput input)
    {
        var asOf = Clock.Now;

        var totalCount = await ContentRepository.GetCountAsync(
            pageId: input.PageId, cultureName: input.CultureName, contentTypeId: input.ContentTypeId,
            status: ContentStatus.Published, publishedBefore: asOf, filter: input.Filter,
            flexFieldConditions: input.FlexFieldConditions);

        var contents = await ContentRepository.GetListAsync(
            pageId: input.PageId, cultureName: input.CultureName, contentTypeId: input.ContentTypeId,
            status: ContentStatus.Published, publishedBefore: asOf, filter: input.Filter,
            flexFieldConditions: input.FlexFieldConditions, maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount, sorting: input.Sorting);

        return new PagedResultDto<ContentDto>(totalCount, contents.Select(MapToDto).ToList());
    }

    public virtual async Task<ListResultDto<ContentDto>> GetTranslationsAsync(
        Guid pageId, Guid contentTypeId, string slug)
    {
        var translations = await ContentRepository.GetTranslationsAsync(pageId, contentTypeId, slug);
        var asOf = Clock.Now;

        return new ListResultDto<ContentDto>(
            translations.Where(c => c.IsPublished(asOf)).Select(MapToDto).ToList());
    }

    protected virtual void EnsurePublished(Content content)
    {
        if (!content.IsPublished(Clock.Now))
        {
            throw new EntityNotFoundException(typeof(Content), content.Id);
        }
    }

    protected virtual ContentDto MapToDto(Content content)
    {
        var dto = ObjectMapper.Map<Content, ContentDto>(content);
        dto.FieldValues = content.FlexFields.ToValueDictionary();
        return dto;
    }
}
