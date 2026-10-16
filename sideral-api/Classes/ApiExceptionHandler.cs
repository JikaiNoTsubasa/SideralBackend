using dependancies.SharedClasses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace sideral_api.Classes;

/// <summary>
/// Convertit toutes les exceptions en réponse application/problem+json.
/// Le message des ApiException est destiné au client ; les exceptions techniques ne renvoient
/// pas leur détail hors environnement de développement.
/// </summary>
internal sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, errorCode) = GetStatusCode(exception);
        string detail;

        if (exception is ApiException)
        {
            _logger.LogWarning("Handled {ExceptionType} on {Method} {Path} -> {Status} (code {ErrorCode}, traceId {TraceId})",
                exception.GetType().Name, ctx.Request.Method, ctx.Request.Path, status, errorCode, ctx.TraceIdentifier);
            detail = exception.Message;
        }
        else
        {
            _logger.LogError(exception, "Unhandled {ExceptionType} on {Method} {Path} -> {Status} (traceId {TraceId})",
                exception.GetType().FullName, ctx.Request.Method, ctx.Request.Path, status, ctx.TraceIdentifier);
            detail = _environment.IsDevelopment() ? exception.Message : "Une erreur inattendue est survenue.";
        }

        var problem = new ProblemDetails
        {
            Type = exception.GetType().FullName?.ToLower().Replace('_', '-') ?? "unknown-type-error",
            Title = title ?? "Internal error",
            Detail = detail,
            Status = status,
            Instance = ctx.Request.Path
        };

        problem.Extensions["errorCode"] = errorCode ?? status.ToString();
        problem.Extensions["timestamp"] = DateTime.UtcNow;
        problem.Extensions["traceId"] = ctx.TraceIdentifier;

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", cancellationToken);
        return true;
    }

    private static (int status, string? title, string? errorCode) GetStatusCode(Exception e)
    {
        return e switch
        {
            ApiException api => (api.Type switch
            {
                ApiException.TypeError.MODEL_ERROR => StatusCodes.Status400BadRequest,
                ApiException.TypeError.BAD_REQUEST => StatusCodes.Status400BadRequest,
                ApiException.TypeError.UNAUTHORIZED => StatusCodes.Status401Unauthorized,
                ApiException.TypeError.FORBIDDEN => StatusCodes.Status403Forbidden,
                ApiException.TypeError.RESSOURCE_NOT_FOUND => StatusCodes.Status404NotFound,
                ApiException.TypeError.CONFLICT => StatusCodes.Status409Conflict,
                ApiException.TypeError.RESSOURCE_ALREADY_EXISTS => StatusCodes.Status409Conflict,
                ApiException.TypeError.TOO_MANY_REQUESTS => StatusCodes.Status429TooManyRequests,
                _ => StatusCodes.Status500InternalServerError
            }, api.Title, api.ErrorCode),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict", null),
            DbUpdateException => (StatusCodes.Status400BadRequest, "Database update error", null),
            BadHttpRequestException bad => (bad.StatusCode, "Bad request", null),
            _ => (StatusCodes.Status500InternalServerError, null, null)
        };
    }
}
