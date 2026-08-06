using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.ExceptionHandling;
using Volo.Abp.Validation;

namespace Dignite.Site.Mcp.Errors;

/// <summary>
/// Turns a domain failure into a tool result an AI client can act on (总体设计 §6.2.4, third footgun).
/// <para>
/// <b>It reuses ABP's own <c>IExceptionToErrorInfoConverter</c></b> - the same converter the HTTP API
/// uses - rather than reading each exception type by hand. That converter already knows which exceptions
/// are safe to surface, already resolves <c>Site:0xxxxx</c> codes through the <c>Site</c> localization
/// resource, and already lifts <c>ValidationResult.MemberNames</c> into a structured list. Re-deriving
/// any of that here would be a second, drifting copy of the error contract.
/// </para>
/// <para>
/// <b>Failures come back as <c>CallToolResult</c> with <c>IsError</c>, not as a thrown
/// <c>McpException</c>.</b> The distinction is the point: a protocol error tells the client's transport
/// something went wrong, while an error <i>result</i> is handed to the model as the outcome of the call
/// it made - which is what lets it fix the field and try again within the same turn.
/// </para>
/// </summary>
public static class McpErrorResultFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Wraps every tool invocation. Registered once in <see cref="SiteMcpModule"/>, so no tool carries
    /// error-shaping code of its own.
    /// <para>
    /// <b>Note what this deliberately does not attempt.</b> A failed tool call can still leave a partially
    /// applied write behind, because the domain managers mutate a tracked entity before validating it
    /// (<c>ContentManager.UpdateAsync</c> sets the status and value bag, then validates) and ABP's
    /// <c>AuditingInterceptor</c> calls <c>SaveChangesAsync</c> from a <c>finally</c> on the way out of a
    /// failed application-service call - so the flush has already happened by the time any handler here
    /// runs. That is framework behaviour shared with the HTTP API, not something this transport
    /// introduces: an MVC action failing persists exactly as much. Rolling back here would be theatre,
    /// since the save is already done, so it is left to be fixed where it actually lives rather than
    /// papered over per-transport.
    /// </para>
    /// </summary>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> CallToolFilter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (request, cancellationToken) =>
        {
            try
            {
                return await next(request, cancellationToken);
            }
            // Cancellation is rethrown rather than reported. The SDK deliberately lets
            // OperationCanceledException escape its own handler, and reporting a disconnected client as a
            // "successful" response would return cleanly to the unit-of-work middleware, which then tries
            // to complete on an already-cancelled token.
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return Create(exception, request.Services);
            }
        };
    }

    public static CallToolResult Create(Exception exception, IServiceProvider? services)
    {
        var error = Describe(exception, services);

        return new CallToolResult
        {
            IsError = true,
            StructuredContent = JsonSerializer.SerializeToElement(
                new McpToolErrorEnvelope { Error = error }, SerializerOptions),
            // The same information again as plain text. Not a fallback to stringifying - the structured
            // payload above is authoritative - but clients are not obliged to surface structuredContent
            // to the model, and a model that cannot see the error cannot correct it.
            Content = { new TextContentBlock { Text = Describe(error) } }
        };
    }

    private static McpToolError Describe(Exception exception, IServiceProvider? services)
    {
        var error = new McpToolError { Kind = KindOf(exception) };

        // The converter is resolved from the request scope, and only there. It is an ASP.NET Core
        // service, so a caller that invokes a tool outside a request (a test) simply gets the exception's
        // own message instead of a localized one - correct information either way.
        var converter = services?.GetService<Volo.Abp.AspNetCore.ExceptionHandling.IExceptionToErrorInfoConverter>();

        if (converter != null)
        {
            var info = converter.Convert(exception, options => options.SendExceptionsDetailsToClients = false);

            error.Code = info.Code;
            error.Message = info.Message ?? exception.Message;
            error.Details = info.Details;

            if (info.ValidationErrors != null)
            {
                error.ValidationErrors = info.ValidationErrors
                    .Select(validationError => new McpToolValidationError
                    {
                        Message = validationError.Message ?? string.Empty,
                        Fields = validationError.Members?.ToList() ?? new()
                    })
                    .ToList();
            }

            return error;
        }

        error.Message = exception.Message;
        error.Code = (exception as IHasErrorCode)?.Code;

        if (exception is AbpValidationException validationException)
        {
            error.ValidationErrors = validationException.ValidationErrors
                .Select(validationError => new McpToolValidationError
                {
                    Message = validationError.ErrorMessage ?? string.Empty,
                    Fields = validationError.MemberNames.ToList()
                })
                .ToList();
        }

        return error;
    }

    /// <summary>
    /// Classifies the failure into the handful of things a client can do differently. Order matters:
    /// the specific types are tested before <see cref="IBusinessException"/>, which several of them
    /// also implement.
    /// </summary>
    private static string KindOf(Exception exception)
    {
        return exception switch
        {
            AbpValidationException => McpToolErrorKinds.Validation,
            McpEntityNotFoundException => McpToolErrorKinds.NotFound,
            EntityNotFoundException => McpToolErrorKinds.NotFound,
            AbpAuthorizationException => McpToolErrorKinds.Forbidden,
            IBusinessException => McpToolErrorKinds.Conflict,
            _ => McpToolErrorKinds.Unknown
        };
    }

    private static string Describe(McpToolError error)
    {
        if (error.ValidationErrors.Count == 0)
        {
            return error.Code == null ? error.Message : $"{error.Message} [{error.Code}]";
        }

        var fields = string.Join("; ", error.ValidationErrors.Select(
            validationError => validationError.Fields.Count == 0
                ? validationError.Message
                : $"{string.Join(", ", validationError.Fields)}: {validationError.Message}"));

        return $"{error.Message} ({fields})";
    }
}
