using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Contracts;

namespace RaceDay.Api.Filters;


// Session based authorisation.
// No session  -> 401 Unauthorized.
// Wrong role  -> 403 Forbidden.
// With no roles supplied, any logged-in user is allowed.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public SessionAuthorizeAttribute(params string[] roles) => _roles = roles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || string.IsNullOrEmpty(role))
        {
            context.Result = new ObjectResult(new ApiMessage("You must be logged in to access this resource."))
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(role))
        {
            context.Result = new ObjectResult(new ApiMessage($"Your role ({role}) is not allowed to access this resource."))
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
