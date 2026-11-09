using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts;
using RaceDay.Mvc.Helpers;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

public class OrganiserController : RaceDayControllerBase
{
    public OrganiserController(RaceDayApiClient api) : base(api) { }

    // =====================================================================
    // Dashboard
    // =====================================================================
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        var result = await Api.GetAsync<List<EventDto>>("api/events/mine");
        if (result.IsUnauthorized) return SessionExpired();
        if (!result.Success) ViewData["Error"] = result.Error;

        var events = (result.Data ?? new()).OrderBy(e => e.EventDate).ToList();
        return View(new OrganiserDashboardVm
        {
            Events = events,
            TotalEnrolments = events.Sum(e => e.EnrolmentCount),
            UpcomingEvents = events.Where(e => e.EventDate.Date >= DateTime.Today).Take(5).ToList()
        });
    }

    // =====================================================================
    // Events: list / create / edit / delete (+ banner upload)
    // =====================================================================
    [HttpGet]
    public async Task<IActionResult> Events()
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        var result = await Api.GetAsync<List<EventDto>>("api/events/mine");
        if (result.IsUnauthorized) return SessionExpired();
        if (!result.Success) ViewData["Error"] = result.Error;
        return View((result.Data ?? new()).OrderBy(e => e.EventDate).ToList());
    }

    [HttpGet]
    public IActionResult CreateEvent()
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;
        return View(new EventFormVm());
    }

    [HttpPost]
    public async Task<IActionResult> CreateEvent(EventFormVm vm)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        var imageError = ValidateImage(vm.Banner);
        if (imageError is not null) ModelState.AddModelError(nameof(vm.Banner), imageError);
        if (!ModelState.IsValid) return View(vm);

        var created = await Api.SendAsync<EventDto>(HttpMethod.Post, "api/events", ToRequest(vm));
        if (created.IsUnauthorized) return SessionExpired();
        if (!created.Success || created.Data is null)
        {
            ModelState.AddModelError("", created.Error ?? "The event could not be created.");
            return View(vm);
        }

        if (vm.Banner is { Length: > 0 })
        {
            var upload = await Api.UploadAsync($"api/events/{created.Data.EventId}/banner", vm.Banner);
            if (!upload.Success)
            {
                TempData["Warning"] = $"Event created, but the banner could not be uploaded: {upload.Error} You can try again by editing the event.";
                return RedirectToAction(nameof(Events));
            }
        }

        TempData["Success"] = "Event created successfully.";
        return RedirectToAction(nameof(Events));
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        var result = await Api.GetAsync<EventDto>($"api/events/{id}");
        if (result.IsUnauthorized) return SessionExpired();
        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.IsNotFound ? "That event could not be found." : result.Error;
            return RedirectToAction(nameof(Events));
        }

        var ev = result.Data;
        return View(new EventFormVm
        {
            EventId = ev.EventId,
            Name = ev.Name,
            Description = ev.Description,
            EventDate = ev.EventDate,
            Location = ev.Location,
            DistanceKm = ev.DistanceKm,
            EventType = ev.EventType,
            ExistingBannerUrl = ev.BannerImageUrl
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditEvent(int id, EventFormVm vm)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;
        vm.EventId = id;

        var imageError = ValidateImage(vm.Banner);
        if (imageError is not null) ModelState.AddModelError(nameof(vm.Banner), imageError);
        if (!ModelState.IsValid) return View(vm);

        var updated = await Api.SendAsync<EventDto>(HttpMethod.Put, $"api/events/{id}", ToRequest(vm));
        if (updated.IsUnauthorized) return SessionExpired();
        if (!updated.Success)
        {
            ModelState.AddModelError("", updated.Error ?? "The event could not be updated.");
            return View(vm);
        }

        if (vm.Banner is { Length: > 0 })
        {
            var upload = await Api.UploadAsync($"api/events/{id}/banner", vm.Banner);
            if (!upload.Success)
            {
                TempData["Warning"] = $"Event updated, but the banner could not be uploaded: {upload.Error}";
                return RedirectToAction(nameof(Events));
            }
        }

        TempData["Success"] = "Event updated successfully.";
        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        var result = await Api.SendAsync(HttpMethod.Delete, $"api/events/{id}");
        if (result.IsUnauthorized) return SessionExpired();

        if (result.Success) TempData["Success"] = "Event deleted.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Events));
    }

    // =====================================================================
    // Categories
    // =====================================================================
    [HttpGet]
    public async Task<IActionResult> Categories(int? eventId)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        var load = await LoadMyEventsAsync(eventId);
        if (load.Expired) return SessionExpired();

        var vm = new CategoriesPageVm { Events = load.Events, SelectedEvent = load.Selected };
        if (!string.IsNullOrEmpty(load.Error)) ViewData["Error"] = load.Error;

        if (load.Selected is not null)
        {
            var categories = await Api.GetAsync<List<CategoryDto>>($"api/events/{load.Selected.EventId}/categories");
            if (categories.IsUnauthorized) return SessionExpired();
            vm.Categories = categories.Data ?? new();
            if (!categories.Success) ViewData["Error"] = categories.Error;
        }
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> AddCategory(int eventId, string? name, string? description)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Please enter a category name (e.g. Under 20, Senior, 10km).";
            return RedirectToAction(nameof(Categories), new { eventId });
        }

        var result = await Api.SendAsync(HttpMethod.Post, $"api/events/{eventId}/categories",
            new CreateCategoryRequest { Name = name, Description = description });
        if (result.IsUnauthorized) return SessionExpired();

        if (result.Success) TempData["Success"] = $"Category '{name.Trim()}' added.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    [HttpPost]
    public async Task<IActionResult> EditCategory(int id, int eventId, string? name, string? description)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "The category name cannot be empty.";
            return RedirectToAction(nameof(Categories), new { eventId });
        }

        var result = await Api.SendAsync(HttpMethod.Put, $"api/categories/{id}",
            new CreateCategoryRequest { Name = name, Description = description });
        if (result.IsUnauthorized) return SessionExpired();

        if (result.Success) TempData["Success"] = "Category updated.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id, int eventId)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        var result = await Api.SendAsync(HttpMethod.Delete, $"api/categories/{id}");
        if (result.IsUnauthorized) return SessionExpired();

        if (result.Success) TempData["Success"] = "Category deleted.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    // =====================================================================
    // Enrolments
    // =====================================================================
    [HttpGet]
    public async Task<IActionResult> Enrolments(int? eventId)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        var load = await LoadMyEventsAsync(eventId);
        if (load.Expired) return SessionExpired();

        var vm = new EnrolmentsPageVm { Events = load.Events, SelectedEvent = load.Selected };
        if (!string.IsNullOrEmpty(load.Error)) ViewData["Error"] = load.Error;

        if (load.Selected is not null)
        {
            var enrolments = await Api.GetAsync<List<EnrolmentDto>>($"api/enrolments/event/{load.Selected.EventId}");
            if (enrolments.IsUnauthorized) return SessionExpired();
            vm.Enrolments = enrolments.Data ?? new();
            if (!enrolments.Success) ViewData["Error"] = enrolments.Error;
        }
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateEnrolmentStatus(int id, int eventId, string status)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        var result = await Api.SendAsync(HttpMethod.Put, $"api/enrolments/{id}/status",
            new UpdateEnrolmentStatusRequest { Status = status });
        if (result.IsUnauthorized) return SessionExpired();

        if (result.Success) TempData["Success"] = $"Enrolment marked as {status}.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Enrolments), new { eventId });
    }

    // =====================================================================
    // Results
    // =====================================================================
    [HttpGet]
    public async Task<IActionResult> Results(int? eventId)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        var load = await LoadMyEventsAsync(eventId);
        if (load.Expired) return SessionExpired();

        var vm = new ResultsPageVm { Events = load.Events, SelectedEvent = load.Selected };
        if (!string.IsNullOrEmpty(load.Error)) ViewData["Error"] = load.Error;

        if (load.Selected is not null)
        {
            var enrolments = await Api.GetAsync<List<EnrolmentDto>>($"api/enrolments/event/{load.Selected.EventId}");
            if (enrolments.IsUnauthorized) return SessionExpired();
            vm.Enrolments = enrolments.Data ?? new();

            var results = await Api.GetAsync<List<ResultDto>>($"api/results/event/{load.Selected.EventId}");
            if (results.IsUnauthorized) return SessionExpired();
            vm.Results = results.Data ?? new();

            if (!enrolments.Success) ViewData["Error"] = enrolments.Error;
            else if (!results.Success) ViewData["Error"] = results.Error;
        }
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> CaptureResult(int enrolmentId, int eventId, string? finishTime, int finishPosition)
    {
        if (RequireRole(RoleNames.Organiser) is { } denied) return denied;

        if (!Format.TryParseFinishTime(finishTime, out var time))
        {
            TempData["Error"] = "Enter the finish time as hh:mm:ss, for example 01:45:30.";
            return RedirectToAction(nameof(Results), new { eventId });
        }
        if (finishPosition < 1)
        {
            TempData["Error"] = "The finishing position must be 1 or higher.";
            return RedirectToAction(nameof(Results), new { eventId });
        }

        var result = await Api.SendAsync(HttpMethod.Post, "api/results",
            new CreateResultRequest { EnrolmentId = enrolmentId, FinishTime = time, FinishPosition = finishPosition });
        if (result.IsUnauthorized) return SessionExpired();

        if (result.Success) TempData["Success"] = "Result captured and published to the participant.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Results), new { eventId });
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private sealed record MyEventsLoad(List<EventDto> Events, EventDto? Selected, string? Error, bool Expired);

    /// <summary>Loads the organiser's own events and works out which one is selected (defaults to the first).</summary>
    private async Task<MyEventsLoad> LoadMyEventsAsync(int? eventId)
    {
        var result = await Api.GetAsync<List<EventDto>>("api/events/mine");
        if (result.IsUnauthorized) return new MyEventsLoad(new(), null, null, true);

        var events = (result.Data ?? new()).OrderBy(e => e.EventDate).ToList();
        var selected = eventId is null
            ? events.FirstOrDefault()
            : events.FirstOrDefault(e => e.EventId == eventId);
        return new MyEventsLoad(events, selected, result.Success ? null : result.Error, false);
    }

    private static CreateEventRequest ToRequest(EventFormVm vm) => new()
    {
        Name = vm.Name,
        Description = vm.Description,
        EventDate = vm.EventDate,
        Location = vm.Location,
        DistanceKm = vm.DistanceKm,
        EventType = vm.EventType
    };
}
