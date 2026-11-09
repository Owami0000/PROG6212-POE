using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts;
using RaceDay.Mvc.Helpers;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

public class ParticipantController : RaceDayControllerBase
{
    public ParticipantController(RaceDayApiClient api) : base(api) { }

    /// <summary>Home page: upcoming events with filtering.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? type, string? search)
    {
        if (RequireRole(RoleNames.Participant) is { } denied) return denied;

        var result = await Api.GetAsync<List<EventDto>>("api/events");
        if (result.IsUnauthorized) return SessionExpired();
        if (!result.Success) ViewData["Error"] = result.Error;

        var events = (result.Data ?? new()).Where(e => e.EventDate.Date >= DateTime.Today);

        if (!string.IsNullOrWhiteSpace(type))
            events = events.Where(e => string.Equals(e.EventType, type, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(search))
            events = events.Where(e =>
                e.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Location.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains(search, StringComparison.OrdinalIgnoreCase));

        return View(new ParticipantHomeVm { Events = events.OrderBy(e => e.EventDate).ToList(), Type = type, Search = search });
    }

    [HttpPost]
    public async Task<IActionResult> EnterEvent(int eventId, int categoryId)
    {
        if (RequireRole(RoleNames.Participant) is { } denied) return denied;

        if (categoryId <= 0)
        {
            TempData["Error"] = "Please choose a category before entering the event.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        var result = await Api.SendAsync(HttpMethod.Post, "api/enrolments",
            new CreateEnrolmentRequest { EventId = eventId, CategoryId = categoryId });
        if (result.IsUnauthorized) return SessionExpired();

        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        TempData["Success"] = "You have entered the event successfully.";
        return RedirectToAction(nameof(MyEvents));
    }

    [HttpGet]
    public async Task<IActionResult> MyEvents()
    {
        if (RequireRole(RoleNames.Participant) is { } denied) return denied;

        var result = await Api.GetAsync<List<EnrolmentDto>>("api/enrolments/mine");
        if (result.IsUnauthorized) return SessionExpired();
        if (!result.Success) ViewData["Error"] = result.Error;
        return View(result.Data ?? new List<EnrolmentDto>());
    }

    [HttpGet]
    public async Task<IActionResult> MyResults()
    {
        if (RequireRole(RoleNames.Participant) is { } denied) return denied;

        var result = await Api.GetAsync<List<ResultDto>>("api/results/mine");
        if (result.IsUnauthorized) return SessionExpired();
        if (!result.Success) ViewData["Error"] = result.Error;
        return View(result.Data ?? new List<ResultDto>());
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        if (RequireRole(RoleNames.Participant) is { } denied) return denied;

        var result = await Api.GetAsync<ProfileDto>("api/profile/me");
        if (result.IsUnauthorized) return SessionExpired();
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Error ?? "Your profile could not be loaded.";
            return RedirectToAction(nameof(Index));
        }
        return View(ToVm(result.Data));
    }

    [HttpPost]
    public async Task<IActionResult> Profile(ProfileVm vm)
    {
        if (RequireRole(RoleNames.Participant) is { } denied) return denied;

        if (!ModelState.IsValid) return await ReloadProfileViewAsync(vm);

        var result = await Api.SendAsync<ProfileDto>(HttpMethod.Put, "api/profile/me", new UpdateProfileRequest
        {
            FirstName = vm.FirstName,
            LastName = vm.LastName,
            PhoneNumber = vm.PhoneNumber
        });
        if (result.IsUnauthorized) return SessionExpired();

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error ?? "Your profile could not be updated.");
            return await ReloadProfileViewAsync(vm);
        }

        HttpContext.Session.SetString(SessionKeys.DisplayName, vm.FirstName);
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    public async Task<IActionResult> UploadProfileImage(IFormFile? image)
    {
        if (RequireRole(RoleNames.Participant) is { } denied) return denied;

        if (image is null || image.Length == 0)
        {
            TempData["Error"] = "Please choose an image to upload.";
            return RedirectToAction(nameof(Profile));
        }

        var invalid = ValidateImage(image);
        if (invalid is not null)
        {
            TempData["Error"] = invalid;
            return RedirectToAction(nameof(Profile));
        }

        // MVC -> API -> Azure Blob Storage. MVC never talks to Azure directly.
        var result = await Api.UploadAsync("api/profile/me/image", image);
        if (result.IsUnauthorized) return SessionExpired();

        if (result.Success) TempData["Success"] = "Your profile picture has been updated.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Profile));
    }

    private async Task<IActionResult> ReloadProfileViewAsync(ProfileVm vm)
    {
        // Re-fill the display-only values that are not posted back with the form.
        var current = await Api.GetAsync<ProfileDto>("api/profile/me");
        if (current.IsUnauthorized) return SessionExpired();
        if (current.Data is not null)
        {
            vm.Email = current.Data.Email;
            vm.Role = current.Data.Role;
            vm.ProfileImageUrl = current.Data.ProfileImageUrl;
        }
        return View(vm);
    }

    private static ProfileVm ToVm(ProfileDto p) => new()
    {
        FirstName = p.FirstName,
        LastName = p.LastName,
        PhoneNumber = p.PhoneNumber,
        Email = p.Email,
        Role = p.Role,
        ProfileImageUrl = p.ProfileImageUrl
    };
}
