using System.Net;

namespace CircDesk.Api.Common;

/// <summary>Base for exceptions that should be surfaced to clients as an RFC 7807 ProblemDetails response.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(HttpStatusCode statusCode, string title, string detail) : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
    }

    public HttpStatusCode StatusCode { get; }
    public string Title { get; }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string detail) : base(HttpStatusCode.NotFound, "Recurso no encontrado", detail)
    {
    }
}

public class ConflictException : DomainException
{
    public ConflictException(string detail) : base(HttpStatusCode.Conflict, "Conflicto de estado", detail)
    {
    }
}

public class ForbiddenException : DomainException
{
    public ForbiddenException(string detail) : base(HttpStatusCode.Forbidden, "Operación no permitida", detail)
    {
    }
}

public class ValidationException : DomainException
{
    public ValidationException(string detail) : base(HttpStatusCode.BadRequest, "Solicitud inválida", detail)
    {
    }
}

public class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException(string detail) : base(HttpStatusCode.Unauthorized, "Credenciales inválidas", detail)
    {
    }
}
