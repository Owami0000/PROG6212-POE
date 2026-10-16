using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(RaceDayDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("register")]
    [SwaggerOperation(Summary = "Register a new account",
        Description = "Creates an Organiser or Participant account. Expects first name, last name, email, password (min 8 chars) and role ('Organiser' or 'Participant'). The password is hashed (PBKDF2) before storage. If 'Registration:OrganiserCode' is configured, Organiser sign-ups must supply the matching OrganiserCode.")]
    [SwaggerResponse(201, "Account created.", typeof(AuthResponse))]
    [SwaggerResponse(400, "Invalid data, invalid role or wrong organiser code.", typeof(ApiMessage))]
    [SwaggerResponse(409, "Email address already registered.", typeof(ApiMessage))]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var roleName = Mapping.MatchAllowed(RoleNames.All, request.Role);
        if (roleName is null)
            return BadRequest(new ApiMessage("Role must be either 'Organiser' or 'Participant'."));

        var requiredCode = _config["Registration:OrganiserCode"];
        if (roleName == RoleNames.Organiser && !string.IsNullOrEmpty(requiredCode) && request.OrganiserCode != requiredCode)
            return BadRequest(new ApiMessage("A valid organiser registration code is required."));

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new ApiMessage("An account with this email already exists."));

        var role = await _db.Roles.SingleAsync(r => r.Name == roleName);
        var user = new AppUser
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber?.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            RoleId = role.RoleId,
            Role = role
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, user.ToAuthResponse());
    }

    [HttpPost("login")]
    [SwaggerOperation(Summary = "Log in",
        Description = "Verifies the email and password. On success a server-side session is created that stores the UserId and Role; the session cookie is used automatically for all later requests.")]
    [SwaggerResponse(200, "Logged in. Returns the authenticated user's details.", typeof(AuthResponse))]
    [SwaggerResponse(400, "Invalid request data.", typeof(ApiMessage))]
    [SwaggerResponse(401, "Incorrect email or password.", typeof(ApiMessage))]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Email == email);

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new ApiMessage("Invalid email or password."));

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.Name);

        return Ok(user.ToAuthResponse());
    }

    [HttpPost("logout")]
    [SwaggerOperation(Summary = "Log out",
        Description = "Clears the server-side session. Protected endpoints return 401 afterwards.")]
    [SwaggerResponse(200, "Logged out.", typeof(ApiMessage))]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new ApiMessage("Logged out."));
    }
}
