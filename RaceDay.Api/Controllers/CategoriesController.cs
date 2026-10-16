using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers;

[Route("api")]
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public CategoriesController(RaceDayDbContext db) => _db = db;

    [HttpGet("events/{eventId:int}/categories")]
    [SessionAuthorize]
    [SwaggerOperation(Summary = "List categories of an event", Description = "Both roles may view the age/distance categories defined for an event.")]
    [SwaggerResponse(200, "Categories for the event.", typeof(IEnumerable<CategoryDto>))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Event not found.", typeof(ApiMessage))]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new ApiMessage("Event not found."));

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId).OrderBy(c => c.Name).ToListAsync();
        return Ok(categories.Select(c => c.ToDto()));
    }

    [HttpPost("events/{eventId:int}/categories")]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "Create a category for an event",
        Description = "Organiser only, and only for events they own. Expects a name (e.g. 'Under 20', 'Senior', '10km') and an optional description.")]
    [SwaggerResponse(201, "Category created.", typeof(CategoryDto))]
    [SwaggerResponse(400, "Invalid data.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser, or not the owner of this event.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Event not found.", typeof(ApiMessage))]
    [SwaggerResponse(409, "A category with that name already exists for this event.", typeof(ApiMessage))]
    public async Task<IActionResult> Create(int eventId, [FromBody] CreateCategoryRequest request)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound(new ApiMessage("Event not found."));
        if (ev.OrganiserId != CurrentUserId) return ForbiddenResult("You can only add categories to events you organise.");

        var name = request.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == eventId && c.Name == name))
            return Conflict(new ApiMessage("This event already has a category with that name."));

        var category = new Category { EventId = eventId, Name = name, Description = request.Description?.Trim() };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return Created($"/api/events/{eventId}/categories", category.ToDto());
    }

    [HttpPut("categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "Update a category", Description = "Organiser only, and only if they own the category's event.")]
    [SwaggerResponse(200, "The updated category.", typeof(CategoryDto))]
    [SwaggerResponse(400, "Invalid data.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser, or not the owner of the event.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Category not found.", typeof(ApiMessage))]
    [SwaggerResponse(409, "Another category with that name already exists for this event.", typeof(ApiMessage))]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound(new ApiMessage("Category not found."));
        if (category.Event.OrganiserId != CurrentUserId) return ForbiddenResult("You can only modify categories of events you organise.");

        var name = request.Name.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == category.EventId && c.Name == name && c.CategoryId != id))
            return Conflict(new ApiMessage("This event already has a category with that name."));

        category.Name = name;
        category.Description = request.Description?.Trim();
        await _db.SaveChangesAsync();
        return Ok(category.ToDto());
    }

    [HttpDelete("categories/{id:int}")]
    [SessionAuthorize(RoleNames.Organiser)]
    [SwaggerOperation(Summary = "Delete a category",
        Description = "Organiser only, and only if they own the category's event. A category that participants have already enrolled in cannot be deleted.")]
    [SwaggerResponse(204, "Category deleted.")]
    [SwaggerResponse(400, "Category has enrolments and cannot be deleted.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    [SwaggerResponse(403, "Not an Organiser, or not the owner of the event.", typeof(ApiMessage))]
    [SwaggerResponse(404, "Category not found.", typeof(ApiMessage))]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound(new ApiMessage("Category not found."));
        if (category.Event.OrganiserId != CurrentUserId) return ForbiddenResult("You can only delete categories of events you organise.");

        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return BadRequest(new ApiMessage("This category already has enrolments and cannot be deleted."));

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
