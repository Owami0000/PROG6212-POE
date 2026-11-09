using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers;

[Route("api/enrolments")]
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    private IQueryable<Enrolment> WithDetails() =>
        _db.Enrolments.Include(e => e.Event).Include(e => e.Category).Include(e => e.Participant);

    [HttpPost]
    [SessionAuthorize(RoleNames.Participant)]
    [SwaggerOperation(Summary = "Enter an event",
        Description = "Participant only. Expects the eventId and a categoryId that belongs to that event. Records the link between the participant, event and category with status 'Pending'.")]
    [SwaggerResponse(201, "Enrolment recorded.", typeof(EnrolmentDto))]
    [SwaggerResponse(400, "Invalid data, category not part of the event, or event already took place.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not a Participant.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Event not found.", typeof(ApiMessage))]
    [SwaggerResponse(409, "Already enrolled in this event.", typeof(ApiMessage))]
    public async Task<IActionResult> Create([FromBody] CreateEnrolmentRequest request)
    {
        var ev = await _db.Events.SingleOrDefaultAsync(e => e.EventId == request.EventId);
        if (ev is null) return NotFound(new ApiMessage("Event not found."));

        if (ev.EventDate.Date < DateTime.UtcNow.Date)
            return BadRequest(new ApiMessage("This event has already taken place."));

        var category = await _db.Categories.SingleOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.EventId == request.EventId);
        if (category is null)
            return BadRequest(new ApiMessage("The selected category does not belong to this event."));

        if (await _db.Enrolments.AnyAsync(e => e.EventId == request.EventId && e.ParticipantId == CurrentUserId))
            return Conflict(new ApiMessage("You are already enrolled in this event."));

        var participant = await _db.Users.FindAsync(CurrentUserId);
        if (participant is null) return Unauthorized(new ApiMessage("Session is no longer valid."));

        var enrolment = new Enrolment
        {
            Event = ev, EventId = ev.EventId,
            Category = category, CategoryId = category.CategoryId,
            Participant = participant, ParticipantId = participant.UserId,
            Status = EnrolmentStatuses.Pending
        };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();
        return Created("/api/enrolments/mine", enrolment.ToDto());
    }

    [HttpGet("mine")]
    [SessionAuthorize(RoleNames.Participant)]
    [SwaggerOperation(Summary = "View my enrolments", Description = "Participant only. Lists the events the logged-in participant has entered.")]
    [SwaggerResponse(200, "The participant's enrolments.", typeof(IEnumerable<EnrolmentDto>))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not a Participant.", typeof(ApiMessage))]
    public async Task<IActionResult> GetMine()
    {
        var list = await WithDetails().AsNoTracking()
            .Where(e => e.ParticipantId == CurrentUserId).OrderBy(e => e.Event.EventDate).ToListAsync();
        return Ok(list.Select(e => e.ToDto()));
    }

    [HttpGet("event/{eventId:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "View all enrolments for one of my events", Description = "Organiser only, and only for events they own.")]
    [SwaggerResponse(200, "Enrolments for the event.", typeof(IEnumerable<EnrolmentDto>))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Event not found.", typeof(ApiMessage))]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiMessage("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return ForbiddenResult("You can only view enrolments for events you organise.");

        var list = await WithDetails().AsNoTracking()
            .Where(e => e.EventId == eventId).OrderBy(e => e.EnrolledAt).ToListAsync();
        return Ok(list.Select(e => e.ToDto()));
    }

    [HttpPut("{id:int}/status")]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "Update an enrolment's status",
        Description = "Organiser only, and only for enrolments in events they own. Status must be Pending, Confirmed or Cancelled.")]
    [SwaggerResponse(200, "The updated enrolment.", typeof(EnrolmentDto))]
    [SwaggerResponse(400, "Invalid status.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser, or not the owner of the event.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Enrolment not found.", typeof(ApiMessage))]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateEnrolmentStatusRequest request)
    {
        var status = Mapping.MatchAllowed(EnrolmentStatuses.All, request.Status);
        if (status is null) return BadRequest(new ApiMessage("Status must be Pending, Confirmed or Cancelled."));

        var enrolment = await WithDetails().SingleOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound(new ApiMessage("Enrolment not found."));
        if (enrolment.Event.OrganiserId != CurrentUserId) return ForbiddenResult("You can only manage enrolments for events you organise.");

        enrolment.Status = status;
        await _db.SaveChangesAsync();
        return Ok(enrolment.ToDto());
    }
}
