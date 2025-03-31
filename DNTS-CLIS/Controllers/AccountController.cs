using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
namespace DNTS_CLIS.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult Logout()
        {
            // Clear all session data
            HttpContext.Session.Clear();

            // Redirect to the login page
            return RedirectToAction("Index", "Login");
        }
    }
}
