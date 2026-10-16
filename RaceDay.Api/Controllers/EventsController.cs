using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers;

[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public EventsController(RaceDayDbContext db) => _db = db;

    [HttpGet]
    [SessionAuthorize]
    [SwaggerOperation(Summary = "List all events", Description = "Both roles may view all events, ordered by date.")]
    [SwaggerResponse(200, "List of events.", typeof(IEnumerable<EventDto>))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    public async Task<IActionResult> GetAll()
    {
        var events = await _db.Events.Include(e => e.Organiser).AsNoTracking()
            .OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToDto()));
    }

    [HttpGet("mine")]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "List my events", Description = "Organiser only. Returns the events created by the logged-in organiser.")]
    [SwaggerResponse(200, "The organiser's events.", typeof(IEnumerable<EventDto>))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser.", typeof(ApiMessage))]
    public async Task<IActionResult> GetMine()
    {
        var events = await _db.Events.Include(e => e.Organiser).AsNoTracking()
            .Where(e => e.OrganiserId == CurrentUserId).OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToDto()));
    }

    [HttpGet("{id:int}")]
    [SessionAuthorize]
    [SwaggerOperation(Summary = "Get one event", Description = "Both roles may view an event by its id.")]
    [SwaggerResponse(200, "The event.", typeof(EventDto))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Event not found.", typeof(ApiMessage))]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events.Include(e => e.Organiser).AsNoTracking().SingleOrDefaultAsync(e => e.EventId == id);
        return ev is null ? NotFound(new ApiMessage("Event not found.")) : Ok(ev.ToDto());
    }

    [HttpPost]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "Create an event",
        Description = "Organiser only. Expects name, description, date, location, distanceKm and eventType (Run, Walk or Cycle). The logged-in organiser becomes the owner.")]
    [SwaggerResponse(201, "Event created.", typeof(EventDto))]
    [SwaggerResponse(400, "Invalid data or event type.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser.", typeof(ApiMessage))]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
    {
        var type = Mapping.MatchAllowed(EventTypes.All, request.EventType);
        if (type is null) return BadRequest(new ApiMessage("EventType must be Run, Walk or Cycle."));

        var organiser = await _db.Users.FindAsync(CurrentUserId);
        if (organiser is null) return Unauthorized(new ApiMessage("Session is no longer valid."));

        var ev = new Event
        {
            Name = request.Name.Trim(), Description = request.Description.Trim(),
            EventDate = request.EventDate, Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm, EventType = type,
            OrganiserId = organiser.UserId, Organiser = organiser
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, ev.ToDto());
    }

    [HttpPut("{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "Update an event", Description = "Organiser only, and only for events they own.")]
    [SwaggerResponse(200, "The updated event.", typeof(EventDto))]
    [SwaggerResponse(400, "Invalid data or event type.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Event not found.", typeof(ApiMessage))]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEventRequest request)
    {
        var type = Mapping.MatchAllowed(EventTypes.All, request.EventType);
        if (type is null) return BadRequest(new ApiMessage("EventType must be Run, Walk or Cycle."));

        var ev = await _db.Events.Include(e => e.Organiser).SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiMessage("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return ForbiddenResult("You can only modify events you organise.");

        ev.Name = request.Name.Trim();
        ev.Description = request.Description.Trim();
        ev.EventDate = request.EventDate;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = type;
        await _db.SaveChangesAsync();
        return Ok(ev.ToDto());
    }

    [HttpDelete("{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "Delete an event",
        Description = "Organiser only, and only for events they own. Also removes the event's categories, enrolments and results.")]
    [SwaggerResponse(204, "Event deleted.")]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Event not found.", typeof(ApiMessage))]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Enrolments).ThenInclude(en => en.Result)
            .SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiMessage("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return ForbiddenResult("You can only delete events you organise.");

        // Enrolments use Restrict in the database, so remove them (and results) first.
        _db.Results.RemoveRange(ev.Enrolments.Where(en => en.Result != null).Select(en => en.Result!));
        _db.Enrolments.RemoveRange(ev.Enrolments);
        _db.Events.Remove(ev); // categories cascade
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
