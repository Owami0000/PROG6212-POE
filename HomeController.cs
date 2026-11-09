using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Mvc.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult AccessDenied() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
    {
        ViewData["Code"] = code;
        return View();
    }
}
