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
    private readonly IBlobStorageService _blobStorage;

    public EventsController(RaceDayDbContext db, IBlobStorageService blobStorage)
    {
        _db = db;
        _blobStorage = blobStorage;
    }

    /// <summary>Maps events to DTOs and adds the number of active (non-cancelled) enrolments to each.</summary>
    private async Task<List<EventDto>> ToDtosAsync(List<Event> events)
    {
        var ids = events.Select(e => e.EventId).ToList();
        var counts = await _db.Enrolments
            .Where(en => ids.Contains(en.EventId) && en.Status != EnrolmentStatuses.Cancelled)
            .GroupBy(en => en.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        return events.Select(e =>
        {
            var dto = e.ToDto();
            dto.EnrolmentCount = counts.GetValueOrDefault(e.EventId);
            return dto;
        }).ToList();
    }

    [HttpGet]
    [SessionAuthorize]
    [SwaggerOperation(Summary = "List all events", Description = "Both roles may view all events, ordered by date.")]
    [SwaggerResponse(200, "List of events.", typeof(IEnumerable<EventDto>))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    public async Task<IActionResult> GetAll()
    {
        var events = await _db.Events.Include(e => e.Organiser).AsNoTracking()
            .OrderBy(e => e.EventDate).ToListAsync();
        return Ok(await ToDtosAsync(events));
    }

    [HttpGet("mine")]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "List my events", Description = "Organiser only. Returns the events created by the logged-in organiser, including the number of enrolments per event.")]
    [SwaggerResponse(200, "The organiser's events.", typeof(IEnumerable<EventDto>))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser.", typeof(ApiMessage))]
    public async Task<IActionResult> GetMine()
    {
        var events = await _db.Events.Include(e => e.Organiser).AsNoTracking()
            .Where(e => e.OrganiserId == CurrentUserId).OrderBy(e => e.EventDate).ToListAsync();
        return Ok(await ToDtosAsync(events));
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
        if (ev is null) return NotFound(new ApiMessage("Event not found."));
        return Ok((await ToDtosAsync(new List<Event> { ev })).First());
    }

    [HttpPost]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "Create an event",
        Description = "Organiser only. Expects name, description, date, location, distanceKm and eventType (Run, Walk or Cycle). The logged-in organiser becomes the owner. Upload a banner afterwards with POST /api/events/{id}/banner.")]
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
        return Ok((await ToDtosAsync(new List<Event> { ev })).First());
    }

    [HttpPost("{id:int}/banner")]
    [SessionAuthorize(RoleNames.Organiser)]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(Summary = "Upload an event banner image",
        Description = "Organiser only, and only for events they own. Expects a multipart/form-data field named 'file' containing a JPG, PNG, GIF or WEBP image (max 5 MB). The API uploads it to Azure Blob Storage, saves the blob URL on the event and returns it.")]
    [SwaggerResponse(200, "Banner stored. Returns the image URL.", typeof(UploadResponse))]
    [SwaggerResponse(400, "Missing, oversized or non-image file.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Event not found.", typeof(ApiMessage))]
    [SwaggerResponse(503, "Image storage is unavailable.", typeof(ApiMessage))]
    public async Task<IActionResult> UploadBanner(int id, IFormFile file)
    {
        var ev = await _db.Events.SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound(new ApiMessage("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return ForbiddenResult("You can only upload banners for events you organise.");

        var error = ImageRules.Validate(file);
        if (error is not null) return BadRequest(new ApiMessage(error));

        string url;
        try { url = await _blobStorage.UploadAsync(file, "event-banners"); }
        catch (BlobStorageException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiMessage("Image storage is currently unavailable. Please try again later."));
        }

        ev.BannerImageUrl = url;
        await _db.SaveChangesAsync();
        return Ok(new UploadResponse { FileUrl = url });
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
