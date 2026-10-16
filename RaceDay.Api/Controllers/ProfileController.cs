using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Filters;
using RaceDay.Api.Services;
using RaceDay.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers;

[Route("api/profile")]
[SessionAuthorize] // any logged-in role
public class ProfileController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    public ProfileController(RaceDayDbContext db) => _db = db;

    [HttpGet("me")]
    [SwaggerOperation(Summary = "View my profile",
        Description = "Returns the profile of the logged-in user (identified from the session, never from a URL id).")]
    [SwaggerResponse(200, "The user's profile.", typeof(ProfileDto))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    public async Task<IActionResult> GetMe()
    {
        var user = await _db.Users.Include(u => u.Role).AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new ApiMessage("Session is no longer valid."));
        }
        return Ok(user.ToProfileDto());
    }

    [HttpPut("me")]
    [SwaggerOperation(Summary = "Update my profile",
        Description = "Updates first name, last name and phone number of the logged-in user only.")]
    [SwaggerResponse(200, "The updated profile.", typeof(ProfileDto))]
    [SwaggerResponse(400, "Invalid data.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Not logged in.", typeof(ApiMessage))]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new ApiMessage("Session is no longer valid."));
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        await _db.SaveChangesAsync();
        return Ok(user.ToProfileDto());
    }
}
