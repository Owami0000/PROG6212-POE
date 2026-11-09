using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts;
using RaceDay.Mvc.Helpers;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

public class AuthController : RaceDayControllerBase
{
    public AuthController(RaceDayApiClient api) : base(api) { }

    private IActionResult RedirectToHome() =>
        string.Equals(CurrentRole, RoleNames.Organiser, StringComparison.OrdinalIgnoreCase)
            ? RedirectToAction("Index", "Organiser")
            : RedirectToAction("Index", "Participant");

    [HttpGet]
    public IActionResult Register() => IsLoggedIn ? RedirectToHome() : View(new RegisterVm());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await Api.SendAsync(HttpMethod.Post, "api/auth/register", new RegisterRequest
        {
            FirstName = vm.FirstName,
            LastName = vm.LastName,
            Email = vm.Email,
            PhoneNumber = vm.PhoneNumber,
            Password = vm.Password,
            Role = vm.Role,
            OrganiserCode = vm.OrganiserCode
        });

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error ?? "Registration failed. Please try again.");
            return View(vm);
        }

        TempData["Success"] = "Registration successful. Please log in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Login() => IsLoggedIn ? RedirectToHome() : View(new LoginVm());

    [HttpPost]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var result = await Api.LoginAsync(new LoginRequest { Email = vm.Email, Password = vm.Password });
        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError("", result.IsUnauthorized
                ? "Invalid email or password."
                : result.Error ?? "Login failed. Please try again.");
            return View(vm);
        }

        var user = result.Data;
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role);
        HttpContext.Session.SetString(SessionKeys.DisplayName, string.IsNullOrWhiteSpace(user.FirstName) ? user.Email : user.FirstName);

        return RedirectToHome();
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await Api.LogoutAsync();
        HttpContext.Session.Clear();
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }
}
