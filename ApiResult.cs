using System.Net;

namespace RaceDay.Mvc.Services;

/// <summary>Outcome of a call to the RaceDay API, with a user-friendly error message on failure.</summary>
public class ApiResult
{
    public bool Success { get; init; }
    public HttpStatusCode StatusCode { get; init; }
    public string? Error { get; init; }

    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
    public bool IsForbidden => StatusCode == HttpStatusCode.Forbidden;
    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;

    public static ApiResult Ok(HttpStatusCode code) => new() { Success = true, StatusCode = code };
    public static ApiResult Fail(HttpStatusCode code, string error) => new() { Success = false, StatusCode = code, Error = error };
}

public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }

    public static ApiResult<T> Ok(HttpStatusCode code, T? data) => new() { Success = true, StatusCode = code, Data = data };
    public new static ApiResult<T> Fail(HttpStatusCode code, string error) => new() { Success = false, StatusCode = code, Error = error };
}
