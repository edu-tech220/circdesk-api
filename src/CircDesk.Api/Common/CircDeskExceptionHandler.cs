using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CircDesk.Api.Common;

/// <summary>Translates DomainException into the matching RFC 7807 ProblemDetails response.</summary>
public class CircDeskExceptionHandler : IExceptionHandler
{
    private readonly ILogger<CircDeskExceptionHandler> _logger;

    public CircDeskExceptionHandler(ILogger<CircDeskExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        _logger.LogWarning(domainException, "Solicitud rechazada: {Title}", domainException.Title);

        var problemDetails = new ProblemDetails
        {
            Status = (int)domainException.StatusCode,
            Title = domainException.Title,
            Detail = domainException.Message,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
