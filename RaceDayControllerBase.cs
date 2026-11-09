using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Helpers;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers;

/// <summary>Shared session / role checks for all MVC controllers.</summary>
public abstract class RaceDayControllerBase : Controller
{
    protected const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] ImageTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };

    protected readonly RaceDayApiClient Api;

    protected RaceDayControllerBase(RaceDayApiClient api) => Api = api;

    protected bool IsLoggedIn => HttpContext.Session.GetInt32(SessionKeys.UserId).HasValue;
    protected string? CurrentRole => HttpContext.Session.GetString(SessionKeys.Role);

    /// <summary>Returns a redirect when not logged in (to Login) or in the wrong role (to AccessDenied); otherwise null.</summary>
    protected IActionResult? RequireRole(string role)
    {
        if (!IsLoggedIn) return RedirectToAction("Login", "Auth");
        if (!string.Equals(CurrentRole, role, StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("AccessDenied", "Home");
        return null;
    }

    protected IActionResult? RequireLogin() => IsLoggedIn ? null : RedirectToAction("Login", "Auth");

    /// <summary>The API no longer recognises this user (e.g. its session timed out): start again at login.</summary>
    protected IActionResult SessionExpired()
    {
        HttpContext.Session.Clear();
        TempData["Error"] = "Your session has expired. Please log in again.";
        return RedirectToAction("Login", "Auth");
    }

    /// <summary>Returns an error message if the uploaded image is not acceptable, otherwise null.</summary>
    protected static string? ValidateImage(IFormFile? file)
    {
        if (file is null || file.Length == 0) return null; // optional
        if (file.Length > MaxImageBytes) return "The image must be 5 MB or smaller.";
        if (!ImageTypes.Contains(file.ContentType.ToLowerInvariant())) return "Only JPG, PNG, GIF or WEBP images are allowed.";
        return null;
    }
}
