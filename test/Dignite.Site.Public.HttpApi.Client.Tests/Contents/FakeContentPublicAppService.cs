using System;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// Records the <see cref="GetContentListInput"/> the controller bound from the query string, so
/// <see cref="ContentListHttpIntegrationTests"/> can compare it with what the client proxy was given - see
/// <see cref="ContentHttpServerTestModule"/>, which registers it as a singleton for exactly that reason.
/// </summary>
public class FakeContentPublicAppService : IContentPublicAppService
{
    public GetContentListInput? LastListInput { get; private set; }

    public Task<PagedResultDto<ContentDto>> GetListAsync(GetContentListInput input)
    {
        LastListInput = input;
        return Task.FromResult(new PagedResultDto<ContentDto>());
    }

    public Task<ContentDto> GetAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<ContentDto> GetBySlugAsync(Guid pageId, string cultureName, string slug)
    {
        throw new NotImplementedException();
    }

    public Task<ListResultDto<ContentDto>> GetTranslationsAsync(Guid pageId, Guid contentTypeId, string slug)
    {
        throw new NotImplementedException();
    }
}
