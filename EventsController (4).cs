using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts;
using RaceDay.Mvc.Helpers;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

/// <summary>Event details page, available to any logged-in user.</summary>
public class EventsController : RaceDayControllerBase
{
    public EventsController(RaceDayApiClient api) : base(api) { }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        if (RequireLogin() is { } denied) return denied;

        var ev = await Api.GetAsync<EventDto>($"api/events/{id}");
        if (ev.IsUnauthorized) return SessionExpired();
        if (!ev.Success || ev.Data is null)
        {
            TempData["Error"] = ev.IsNotFound ? "That event could not be found." : ev.Error;
            return RedirectToAction("Index", CurrentRole == RoleNames.Organiser ? "Organiser" : "Participant");
        }

        var vm = new EventDetailsVm
        {
            Event = ev.Data,
            IsParticipant = string.Equals(CurrentRole, RoleNames.Participant, StringComparison.OrdinalIgnoreCase)
        };

        var categories = await Api.GetAsync<List<CategoryDto>>($"api/events/{id}/categories");
        if (categories.IsUnauthorized) return SessionExpired();
        vm.Categories = categories.Data ?? new();
        if (!categories.Success) ViewData["Error"] = categories.Error;

        if (vm.IsParticipant)
        {
            var mine = await Api.GetAsync<List<EnrolmentDto>>("api/enrolments/mine");
            if (mine.IsUnauthorized) return SessionExpired();
            vm.MyEnrolment = mine.Data?.FirstOrDefault(e => e.EventId == id);
        }

        return View(vm);
    }
}
