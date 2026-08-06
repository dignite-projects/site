using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Mcp.Contents;
using Dignite.Site.Mcp.Errors;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Shouldly;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Validation;
using Xunit;

namespace Dignite.Site.Mcp;

/// <summary>
/// A failed tool call has to tell the client WHICH field and WHY, so the model can correct itself in one
/// turn (总体设计 §6.2.4, third footgun). Flattening the gate's diagnostics into one sentence throws away
/// the only part a model can act on.
/// </summary>
public class McpErrorMapping_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly ContentTools _contentTools;

    public McpErrorMapping_Tests()
    {
        _contentTools = GetRequiredService<ContentTools>();
    }

    [Fact]
    public void Should_Carry_Every_Field_Name_Out_Of_A_Validation_Failure()
    {
        var exception = new AbpValidationException(
            "One or more field values are not valid for this content type.",
            new List<ValidationResult>
            {
                new("Title is required.", new[] { "title" }),
                new("Views must be a number.", new[] { "views" })
            });

        var error = Describe(exception);

        error.Kind.ShouldBe(McpToolErrorKinds.Validation);
        error.ValidationErrors.SelectMany(validationError => validationError.Fields)
            .ShouldBe(new[] { "title", "views" }, ignoreOrder: true);
        error.ValidationErrors.Single(validationError => validationError.Fields.Contains("title"))
            .Message.ShouldBe("Title is required.");
    }

    /// <summary>
    /// The end-to-end version of the above: a real write rejected by <c>ContentManager</c>'s field
    /// validator arrives at the client with the field named.
    /// </summary>
    [Fact]
    public async Task Should_Turn_A_Real_Missing_Required_Field_Into_A_Per_Field_Error_Result()
    {
        var exception = await Should.ThrowAsync<AbpValidationException>(async () =>
            await _contentTools.CreateContentAsync(
                page: "blog",
                contentType: "post-article",
                cultureName: SiteTestData.EnglishCulture,
                slug: "needs-a-title",
                publishTime: SiteTestData.PublishTime));

        // Through the REAL converter, not the services: null fallback. The two branches build
        // ValidationErrors from different sources - RemoteServiceValidationErrorInfo.Members versus
        // ValidationResult.MemberNames - so only this one says anything about what a client receives.
        var result = McpErrorResultFactory.Create(exception, GetRequiredService<IServiceProvider>());

        result.IsError.ShouldBe(true);

        var error = ReadError(result.StructuredContent!.Value);
        error.Kind.ShouldBe(McpToolErrorKinds.Validation);
        error.ValidationErrors.SelectMany(validationError => validationError.Fields).ShouldContain("title");

        // And the text block, which is all some clients show the model.
        result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>()
            .Single().Text.ShouldContain("title");
    }

    /// <summary>
    /// A business rule keeps its error code, which is stable across languages - unlike the message, which
    /// is localized. A client can branch on <c>Site:040001</c>; it cannot branch on a sentence.
    /// </summary>
    [Fact]
    public async Task Should_Carry_The_Error_Code_Of_A_Duplicate_Slug()
    {
        var exception = await Should.ThrowAsync<ContentSlugAlreadyExistException>(async () =>
            await _contentTools.CreateContentAsync(
                page: "blog",
                contentType: "post-article",
                cultureName: SiteTestData.EnglishCulture,
                slug: SiteTestData.TripSlug,
                fieldValues: new Dictionary<string, object?> { ["title"] = "Colliding" },
                publishTime: SiteTestData.PublishTime));

        var error = Describe(exception);

        error.Kind.ShouldBe(McpToolErrorKinds.Conflict);
        error.Code.ShouldBe(SiteErrorCodes.ContentSlugAlreadyExists);
    }

    [Fact]
    public void Should_Classify_An_Unknown_Name_As_NotFound()
    {
        var error = Describe(new McpEntityNotFoundException("page", "blogg"));

        error.Kind.ShouldBe(McpToolErrorKinds.NotFound);
        error.Code.ShouldBe(SiteMcpErrorCodes.NameNotFound);
        error.Message.ShouldContain("get_site_schema");
    }

    /// <summary>
    /// KindOf's ordering comment says several types "also implement IBusinessException" - McpEntityNotFoundException
    /// (a UserFriendlyException) is one, since UserFriendlyException derives from BusinessException. This
    /// pins that its own arm still wins over the IBusinessException fallback despite that, so a rename or
    /// reorder of the switch cannot silently downgrade every not-found into a generic "conflict".
    /// </summary>
    [Fact]
    public void Should_Not_Let_McpEntityNotFoundException_Fall_Through_To_The_Conflict_Arm()
    {
        Describe(new McpEntityNotFoundException("page", "blogg")).Kind.ShouldNotBe(McpToolErrorKinds.Conflict);
    }

    /// <summary>
    /// The one auth-specific arm KindOf carries: AbpAuthorizationException implements neither
    /// IBusinessException nor any of the other special-cased types, so without its own arm it would fall to
    /// Unknown - losing the one piece of information that actually matters here, that retrying is pointless.
    /// </summary>
    [Fact]
    public void Should_Classify_An_Authorization_Failure_As_Forbidden()
    {
        var error = Describe(new AbpAuthorizationException("Not allowed to call this tool."));

        error.Kind.ShouldBe(McpToolErrorKinds.Forbidden);
    }

    /// <summary>
    /// ABP's own EntityNotFoundException, distinct from McpEntityNotFoundException: it is what an id-keyed
    /// operation throws when the row is gone by the time it runs, even though the name it was addressed by
    /// resolved cleanly moments earlier - a concurrent delete landing between
    /// <c>SiteMcpNameResolver.GetContentAsync</c> and <c>ContentAdminAppService.UpdateAsync</c>/
    /// <c>DeleteAsync</c>. It implements neither IBusinessException nor IUserFriendlyException, so it needs
    /// its own arm too, or it falls to Unknown despite "re-read the schema and retry" being exactly the
    /// right client behaviour.
    /// </summary>
    [Fact]
    public void Should_Classify_Abps_Own_EntityNotFoundException_As_NotFound()
    {
        var error = Describe(new EntityNotFoundException(typeof(Content), Guid.NewGuid()));

        error.Kind.ShouldBe(McpToolErrorKinds.NotFound);
    }

    /// <summary>
    /// A second BusinessException subtype through the same IBusinessException fallback
    /// ContentSlugAlreadyExistException already exercises below, so that arm's correctness is not resting
    /// on a single example. Reached in practice by reverting a content that is Published with a future
    /// PublishTime back to Draft without also giving a non-future one - see
    /// ContentTools_Tests.Should_Reject_Reverting_A_Scheduled_Content_To_Draft_Without_Also_Clearing_Its_Future_Time
    /// for the end-to-end path that throws it.
    /// </summary>
    [Fact]
    public void Should_Classify_A_Draft_With_A_Future_Publish_Time_As_Conflict()
    {
        var error = Describe(new ContentDraftCannotHaveFuturePublishTimeException(DateTime.UtcNow.AddDays(30)));

        error.Kind.ShouldBe(McpToolErrorKinds.Conflict);
        error.Code.ShouldBe(SiteErrorCodes.ContentDraftCannotHaveFuturePublishTime);
    }

    /// <summary>
    /// The catch-all. An exception type the tool layer never anticipated must still come back as a
    /// well-formed error result (McpErrorResultFactory's try/catch sees everything), classified as Unknown
    /// rather than mis-reported as one of the specific, actionable kinds a client might treat as safe to
    /// retry blindly or ignore outright.
    /// </summary>
    [Fact]
    public void Should_Classify_An_Unrecognized_Exception_As_Unknown()
    {
        var error = Describe(new InvalidOperationException("Something the tool layer never anticipated."));

        error.Kind.ShouldBe(McpToolErrorKinds.Unknown);
    }

    /// <summary>
    /// The same assertion, but through ABP's real <c>IExceptionToErrorInfoConverter</c> rather than the
    /// no-service fallback - which is the path every actual request takes.
    /// <para>
    /// This distinction is the whole reason the test exists. The converter only passes an exception's own
    /// message through for <c>IUserFriendlyException</c>; anything else is localized by its error code or
    /// replaced with "An internal error occurred during your request!". This code has no localization
    /// entry on purpose, so a plain <c>BusinessException</c> here would have its "call get_site_schema"
    /// text silently discarded on every real call while the fallback-path test above kept passing.
    /// </para>
    /// </summary>
    [Fact]
    public void Should_Keep_The_Not_Found_Guidance_When_It_Goes_Through_Abps_Real_Converter()
    {
        var services = GetRequiredService<IServiceProvider>();

        // Proves the converter really is in play, not silently absent.
        GetRequiredService<IExceptionToErrorInfoConverter>().ShouldNotBeNull();

        var result = McpErrorResultFactory.Create(
            new McpEntityNotFoundException("page", "blogg"), services);

        var error = ReadError(result.StructuredContent!.Value);

        error.Code.ShouldBe(SiteMcpErrorCodes.NameNotFound);
        error.Message.ShouldContain("get_site_schema");
        error.Message.ShouldNotContain("internal error");
    }

    /// <summary>
    /// The domain's own exceptions take the other route: they are localized by error code, so their codes
    /// must have entries or they would degrade to the same generic message.
    /// </summary>
    [Fact]
    public async Task Should_Localize_A_Domain_Error_Code_Through_The_Real_Converter()
    {
        var services = GetRequiredService<IServiceProvider>();

        var exception = await Should.ThrowAsync<ContentSlugAlreadyExistException>(async () =>
            await _contentTools.CreateContentAsync(
                page: "blog",
                contentType: "post-article",
                cultureName: SiteTestData.EnglishCulture,
                slug: SiteTestData.TripSlug,
                fieldValues: new Dictionary<string, object?> { ["title"] = "Colliding" },
                publishTime: SiteTestData.PublishTime));

        var error = ReadError(McpErrorResultFactory.Create(exception, services).StructuredContent!.Value);

        error.Code.ShouldBe(SiteErrorCodes.ContentSlugAlreadyExists);
        error.Message.ShouldNotContain("internal error");
    }

    /// <summary>
    /// The structured payload is authoritative, but a client is not obliged to show it to the model - so
    /// the text block has to carry the same field names, not a generic apology.
    /// </summary>
    [Fact]
    public void Should_Repeat_The_Field_Names_In_The_Text_Content()
    {
        var result = McpErrorResultFactory.Create(
            new AbpValidationException(
                "Invalid.",
                new List<ValidationResult> { new("Title is required.", new[] { "title" }) }),
            GetRequiredService<IServiceProvider>());

        var text = result.Content
            .OfType<ModelContextProtocol.Protocol.TextContentBlock>()
            .Single().Text;

        text.ShouldContain("title");
        text.ShouldContain("Title is required.");
    }

    /// <summary>
    /// Always through the real service provider. Passing <c>services: null</c> takes a fallback branch
    /// that no HTTP request ever reaches - it reads the exception's own message and MemberNames, while
    /// production goes through ABP's converter and reads localized text and
    /// <c>RemoteServiceValidationErrorInfo.Members</c>. A test on the fallback can pass while the real
    /// path returns something else entirely, which is exactly how the not-found guidance was lost.
    /// </summary>
    /// <summary>
    /// The filter itself, not just the factory it delegates to. Everything else in this file and in
    /// <c>ContentTools_Tests</c> asserts a thrown exception - a shape production never produces, because
    /// this filter converts it. If the registration in <c>SiteMcpModule</c> were dropped or an SDK upgrade
    /// renamed the hook, every domain failure would reach the client as a protocol-level fault with no
    /// structured content, and nothing else here would notice.
    /// </summary>
    [Fact]
    public async Task Should_Convert_A_Thrown_Exception_Into_An_Error_Result()
    {
        var handler = McpErrorResultFactory.CallToolFilter(
            (_, _) => throw new McpEntityNotFoundException("page", "blogg"));

        var result = await InvokeAsync(handler);

        result.IsError.ShouldBe(true);
        ReadError(result.StructuredContent!.Value).Kind.ShouldBe(McpToolErrorKinds.NotFound);
    }

    /// <summary>A call that succeeds must pass straight through, untouched.</summary>
    [Fact]
    public async Task Should_Pass_A_Successful_Result_Through_Unchanged()
    {
        var expected = new CallToolResult();

        var result = await InvokeAsync(McpErrorResultFactory.CallToolFilter((_, _) => new(expected)));

        result.ShouldBeSameAs(expected);
        result.IsError.ShouldNotBe(true);
    }

    /// <summary>
    /// Cancellation is deliberately NOT converted. Reporting a disconnected client as a successful
    /// response returns cleanly to the unit-of-work middleware, which then completes on an already
    /// cancelled token.
    /// </summary>
    [Fact]
    public async Task Should_Let_Cancellation_Propagate_Rather_Than_Reporting_It()
    {
        var handler = McpErrorResultFactory.CallToolFilter(
            (_, ct) => throw new OperationCanceledException(ct));

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await InvokeAsync(handler, new CancellationToken(canceled: true)));
    }

    /// <summary>
    /// Drives a filter handler without an <c>McpServer</c>. <c>RequestContext</c> cannot be constructed
    /// without one, and the filter only ever reads <c>Services</c>, so a null server is passed through
    /// unused rather than mocked.
    /// </summary>
    private async Task<CallToolResult> InvokeAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> handler,
        CancellationToken cancellationToken = default)
    {
        var request = (RequestContext<CallToolRequestParams>)FormatterServices
            .GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));

        request.Services = GetRequiredService<IServiceProvider>();

        return await handler(request, cancellationToken);
    }

    private McpToolError Describe(Exception exception)
    {
        return ReadError(McpErrorResultFactory
            .Create(exception, GetRequiredService<IServiceProvider>()).StructuredContent!.Value);
    }

    private static McpToolError ReadError(JsonElement structuredContent)
    {
        return JsonSerializer
            .Deserialize<McpToolErrorEnvelope>(structuredContent, new JsonSerializerOptions(JsonSerializerDefaults.Web))!
            .Error;
    }
}
