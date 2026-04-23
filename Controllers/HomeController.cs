using Microsoft.AspNetCore.Mvc;

namespace CollaborativeBoard.Controllers;

public sealed class HomeController : Controller
{
    [Route("/Home/Error")]
    public IActionResult Error()
    {
        return View();
    }
}
