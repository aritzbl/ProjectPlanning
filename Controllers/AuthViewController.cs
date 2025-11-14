using Microsoft.AspNetCore.Mvc;

namespace ProjectPlanning.Controllers
{
    public class AuthViewController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }

        public IActionResult OngMenu()
        {
            return View();
        }

        public IActionResult ProjectList()
        {
            return View();
        }

        public IActionResult Logout()
        {
            return RedirectToAction("Login");
        }
    }
}