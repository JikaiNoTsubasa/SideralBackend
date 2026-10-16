namespace dependancies.SharedClasses;

public class ApiException : Exception
{
    public TypeError Type { get; }
    public string Title { get; }
    public string? ErrorCode { get; }

    public ApiException(string title, string message, TypeError type, string? errorCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Type = type;
        Title = title;
        ErrorCode = errorCode;
    }

    public enum TypeError
    {
        UNKNOWN,
        INTERNAL_ERROR,
        RESSOURCE_NOT_FOUND,
        CONFLICT,
        MODEL_ERROR,
        RESSOURCE_ALREADY_EXISTS,
        BAD_REQUEST,
        FORBIDDEN,
        UNAUTHORIZED,
        TOO_MANY_REQUESTS
    }
}
