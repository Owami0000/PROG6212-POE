using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Filters;
using RaceDay.Contracts;

namespace RaceDay.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    //Id of the logged-in user, read from the server-side session.
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    protected ObjectResult ForbiddenResult(string message) =>
        StatusCode(StatusCodes.Status403Forbidden, new ApiMessage(message));
}
