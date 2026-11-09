using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Contracts;

namespace RaceDay.Mvc.Services;

/// <summary>
/// The only way the MVC app talks to data: every operation goes through the Part 2 API.
/// The API's session cookie is captured at login, stored in the MVC session and sent with every later request.
/// </summary>
public class RaceDayApiClient
{
    public const string ApiCookieName = ".RaceDay.Session";   // cookie name used by RaceDay.Api
    private const string SessionKey = "ApiSessionCookie";
    private const string Unavailable = "The RaceDay service is currently unavailable. Please try again in a moment.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _factory;
    private readonly IHttpContextAccessor _accessor;
    private readonly ILogger<RaceDayApiClient> _logger;

    public RaceDayApiClient(IHttpClientFactory factory, IHttpContextAccessor accessor, ILogger<RaceDayApiClient> logger)
    {
        _factory = factory;
        _accessor = accessor;
        _logger = logger;
    }

    private ISession? Session => _accessor.HttpContext?.Session;

    private sealed record RawResponse(HttpStatusCode StatusCode, bool Success, string Body, string? SessionCookie, string? NetworkError);

    // ---------------- public API ----------------

    public Task<ApiResult<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url);

    public async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var request = CreateRequest(method, url, body);
        return ToResult<T>(await ExecuteAsync(request));
    }

    public async Task<ApiResult> SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var request = CreateRequest(method, url, body);
        var raw = await ExecuteAsync(request);
        return raw.Success ? ApiResult.Ok(raw.StatusCode) : ApiResult.Fail(raw.StatusCode, raw.NetworkError ?? ParseError(raw.StatusCode, raw.Body));
    }

    /// <summary>Sends an image to the API as multipart/form-data (field name "file"). The API stores it in Azure Blob Storage.</summary>
    public async Task<ApiResult<UploadResponse>> UploadAsync(string url, IFormFile file)
    {
        using var request = CreateRequest(HttpMethod.Post, url, null);
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        form.Add(fileContent, "file", file.FileName);
        request.Content = form;
        return ToResult<UploadResponse>(await ExecuteAsync(request));
    }

    /// <summary>Logs in through the API and keeps the API session cookie in the MVC session.</summary>
    public async Task<ApiResult<AuthResponse>> LoginAsync(LoginRequest login)
    {
        Session?.Remove(SessionKey);
        using var request = CreateRequest(HttpMethod.Post, "api/auth/login", login);
        var raw = await ExecuteAsync(request, captureCookie: true);
        var result = ToResult<AuthResponse>(raw);

        if (result.Success)
        {
            if (string.IsNullOrWhiteSpace(raw.SessionCookie) || result.Data is null)
                return ApiResult<AuthResponse>.Fail(HttpStatusCode.BadGateway, "Login succeeded but no session could be created. Please try again.");
            Session?.SetString(SessionKey, raw.SessionCookie);
        }
        return result;
    }

    /// <summary>Ends the API session and forgets the stored cookie.</summary>
    public async Task LogoutAsync()
    {
        await SendAsync(HttpMethod.Post, "api/auth/logout");
        Session?.Remove(SessionKey);
    }

    // ---------------- internals ----------------

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        var cookie = Session?.GetString(SessionKey);
        if (!string.IsNullOrWhiteSpace(cookie))
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        return request;
    }

    private async Task<RawResponse> ExecuteAsync(HttpRequestMessage request, bool captureCookie = false)
    {
        try
        {
            var client = _factory.CreateClient("RaceDayApi");
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            return new RawResponse(response.StatusCode, response.IsSuccessStatusCode, body,
                captureCookie ? ExtractSessionCookie(response) : null, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogError(ex, "Could not reach the RaceDay API ({Method} {Url}).", request.Method, request.RequestUri);
            return new RawResponse(HttpStatusCode.ServiceUnavailable, false, string.Empty, null, Unavailable);
        }
    }

    private ApiResult<T> ToResult<T>(RawResponse raw)
    {
        if (!raw.Success)
            return ApiResult<T>.Fail(raw.StatusCode, raw.NetworkError ?? ParseError(raw.StatusCode, raw.Body));

        if (string.IsNullOrWhiteSpace(raw.Body))
            return ApiResult<T>.Ok(raw.StatusCode, default);

        try
        {
            return ApiResult<T>.Ok(raw.StatusCode, JsonSerializer.Deserialize<T>(raw.Body, JsonOptions));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Unexpected API response format.");
            return ApiResult<T>.Fail(HttpStatusCode.BadGateway, "The RaceDay service returned an unexpected response.");
        }
    }

    /// <summary>The API's session cookie: ".RaceDay.Session=value" (without Path/HttpOnly/etc).</summary>
    private static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return null;
        var header = values.FirstOrDefault(v => v.StartsWith(ApiCookieName + "=", StringComparison.OrdinalIgnoreCase));
        return header?.Split(';', 2)[0];
    }

    /// <summary>Turns an API error body into a short, friendly message (never raw JSON or stack traces).</summary>
    private static string ParseError(HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    return message.GetString()!;

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = errors.EnumerateObject()
                        .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array ? p.Value.EnumerateArray() : Enumerable.Empty<JsonElement>())
                        .Select(e => e.GetString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Take(3);
                    var joined = string.Join(" ", messages);
                    if (joined.Length > 0) return joined;
                }
            }
        }
        catch (JsonException) { /* fall through to generic message */ }

        return status switch
        {
            HttpStatusCode.Unauthorized => "Please log in to continue.",
            HttpStatusCode.Forbidden => "You do not have permission to do that.",
            HttpStatusCode.NotFound => "The requested item could not be found.",
            HttpStatusCode.BadRequest => "Some of the information provided is not valid.",
            HttpStatusCode.Conflict => "That item already exists.",
            HttpStatusCode.ServiceUnavailable => Unavailable,
            _ => "Something went wrong. Please try again."
        };
    }
}
