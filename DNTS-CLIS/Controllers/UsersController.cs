using DNTS_CLIS.Data;
using DNTS_CLIS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Clis5.Controllers
{
    public class UsersController : Controller
    {
        private readonly DNTS_CLISContext _context;

        public UsersController(DNTS_CLISContext context)
        {
            _context = context;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var users = await _context.User.ToListAsync();
            return View(users);

        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.User
                .FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            var laboratories = _context.Laboratories.ToList();
            ViewBag.Laboratories = laboratories ?? new List<Laboratories>();

            var roles = new List<SelectListItem>
    {
        new SelectListItem { Value = "Supervisor", Text = "Supervisor" },
        new SelectListItem { Value = "Property Custodian", Text = "Property Custodian" },
        new SelectListItem { Value = "Technical Assistant", Text = "Technical Assistant" }
    };

            ViewBag.Roles = roles;
            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UserId,FirstName,LastName,BirthDate,Age,Email,Status,TemporaryPassword,Role,AssignLaboratory,Username,Password")] User user)
        {
            if (_context.User.Any(u => u.Username == user.Username))
            {
                ModelState.AddModelError("Username", "Username already exists.");
                ViewBag.Laboratories = _context.Laboratories.ToList();
                ViewBag.Roles = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Supervisor", Text = "Supervisor" },
                    new SelectListItem { Value = "Property Custodian", Text = "Property Custodian" },
                    new SelectListItem { Value = "Technical Assistant", Text = "Technical Assistant" }
                };

                return View(user);
            }
            if (user.Role == "Supervisor")
            {
                user.AssignLaboratory = "N/A";
                user.TemporaryPassword = "N/A";
            }
            else if (user.Role == "Property Custodian")
            {
                user.AssignLaboratory = "N/A";
                user.TemporaryPassword = "N/A";
            }
            if (ModelState.IsValid)
            {
                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Laboratories = _context.Laboratories.ToList();
            ViewBag.Roles = new List<SelectListItem>
    {
        new SelectListItem { Value = "Supervisor", Text = "Supervisor" },
        new SelectListItem { Value = "Property Custodian", Text = "Property Custodian" },
        new SelectListItem { Value = "Technical Assistant", Text = "Technical Assistant" }
    };

            return View(user);
        }


        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.User.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            ViewBag.Laboratories = _context.Laboratories.ToList() ?? new List<Laboratories>();
            return View(user);
        }

        // POST: Users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("UserId,FirstName,LastName,BirthDate,Age,Email,Role,AssignLaboratory,Username,Password")] User user)
        {
            if (id != user.UserId)
            {
                return NotFound();
            }

            // Get the existing user to preserve the Status
            var existingUser = await _context.User.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id);
            if (existingUser == null)
            {
                return NotFound();
            }

            // Preserve the Status value
            user.Status = existingUser.Status;

            // Handle Supervisor role
            if (user.Role == "Supervisor" || user.Role == "Property Custodian")
            {
                user.AssignLaboratory = "N/A";
            }


            // Update the entity
            _context.Update(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        private bool UserExists(int id)
        {
            return _context.User.Any(e => e.UserId == id);
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _context.User
                .FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.User.FindAsync(id);
            if (user != null)
            {
                // Soft delete: mark as Inactive
                user.Status = "Inactive";
                _context.Update(user);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public JsonResult IsUsernameAvailable(string username)
        {
            bool exists = _context.User.Any(u => u.Username == username);
            return Json(!exists); 
        }
     }
}
