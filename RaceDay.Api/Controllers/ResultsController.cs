using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers;

[Route("api/results")]
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public ResultsController(RaceDayDbContext db) => _db = db;

    private IQueryable<Result> WithDetails() =>
        _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e.Event)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category)
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant);

    [HttpPost]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "Capture a result",
        Description = "Organiser only. Records the finish time (hh:mm:ss) and finishing position for an enrolment. The organiser must own the event the enrolment belongs to.")]
    [SwaggerResponse(201, "Result recorded.", typeof(ResultDto))]
    [SwaggerResponse(400, "Invalid data or enrolment is cancelled.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser, or not the owner of the event.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Enrolment not found.", typeof(ApiMessage))]
    [SwaggerResponse(409, "A result already exists for this enrolment.", typeof(ApiMessage))]
    public async Task<IActionResult> Create([FromBody] CreateResultRequest request)
    {
        if (request.FinishTime <= TimeSpan.Zero)
            return BadRequest(new ApiMessage("FinishTime must be greater than zero (format hh:mm:ss)."));

        var enrolment = await _db.Enrolments
            .Include(e => e.Event).Include(e => e.Category).Include(e => e.Participant)
            .SingleOrDefaultAsync(e => e.EnrolmentId == request.EnrolmentId);
        if (enrolment is null) return NotFound(new ApiMessage("Enrolment not found."));
        if (enrolment.Event.OrganiserId != CurrentUserId) return ForbiddenResult("You can only capture results for events you organise.");
        if (enrolment.Status == EnrolmentStatuses.Cancelled)
            return BadRequest(new ApiMessage("Cannot capture a result for a cancelled enrolment."));

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == enrolment.EnrolmentId))
            return Conflict(new ApiMessage("A result has already been captured for this enrolment."));

        var result = new Result
        {
            EnrolmentId = enrolment.EnrolmentId,
            Enrolment = enrolment,
            FinishTime = request.FinishTime,
            FinishPosition = request.FinishPosition
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();
        return Created("/api/results/mine", result.ToDto());
    }

    [HttpGet("mine")]
    [SessionAuthorize(RoleNames.Participant)]
    [SwaggerOperation(Summary = "View my results", Description = "Participant only. Returns the logged-in participant's own results.")]
    [SwaggerResponse(200, "The participant's results.", typeof(IEnumerable<ResultDto>))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not a Participant.", typeof(ApiMessage))]
    public async Task<IActionResult> GetMine()
    {
        var list = await WithDetails().AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == CurrentUserId)
            .OrderBy(r => r.Enrolment.Event.EventDate).ToListAsync();
        return Ok(list.Select(r => r.ToDto()));
    }

    [HttpGet("event/{eventId:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "View all results for one of my events", Description = "Organiser only, and only for events they own. Ordered by finishing position.")]
    [SwaggerResponse(200, "Results for the event.", typeof(IEnumerable<ResultDto>))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Event not found.", typeof(ApiMessage))]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiMessage("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return ForbiddenResult("You can only view results for events you organise.");

        var list = await WithDetails().AsNoTracking()
            .Where(r => r.Enrolment.EventId == eventId).OrderBy(r => r.FinishPosition).ToListAsync();
        return Ok(list.Select(r => r.ToDto()));
    }
}
