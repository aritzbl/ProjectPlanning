using Microsoft.AspNetCore.Mvc;

namespace ProjectPlanning.Web.Controllers
{
    public class AuthViewController : Controller
    {
        public IActionResult Register()
        {
            return View(); // Busca Views/AuthView/Register.cshtml
        }

        public IActionResult Login()
        {
            return View(); // Busca Views/AuthView/Login.cshtml
        }

        public IActionResult OngMenu()
        {
            return View(); // Busca Views/AuthView/OngMenu.cshtml
        }

        public IActionResult ProjectList()
        {
            return View(); // Busca Views/AuthView/ProjectList.cshtml
        }
    }
}
