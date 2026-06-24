using DNTS_CLIS.Data;
using DNTS_CLIS.Models;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NuGet.DependencyResolver;
using System.ComponentModel.DataAnnotations;
using System.Drawing.Drawing2D;
using System.Net.Mail;
using System.Net;
using System.Security.Principal;

namespace DNTS_CLIS.Controllers
{
    public class LoginController : Controller
    {
        private readonly DNTS_CLISContext _context;
        private readonly EmailService _emailService;
        private readonly IConfiguration _config;

        public LoginController(DNTS_CLISContext context, EmailService emailService, IConfiguration config)
        {
            _context = context;
            _emailService = emailService;
            _config = config;
        }

        [HttpGet]
        public IActionResult Index()
        {
            EnsureTrackRecordsTableExists();
            return View();
        }

        // If the table does not exist, automatically create it (PostgreSQL Compatible)
        private void EnsureTrackRecordsTableExists()
        {
            string query = @"
            CREATE TABLE IF NOT EXISTS ""TrackRecords"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TrackNo"" VARCHAR(255) NOT NULL,
                ""ReceiverName"" VARCHAR(255) NOT NULL,
                ""CreatedDate"" TIMESTAMP NOT NULL
            );";
            _context.Database.ExecuteSqlRaw(query);

            string createTableQuery = @"
            CREATE TABLE IF NOT EXISTS ""AssignedLaboratories"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""TrackNo"" VARCHAR(255) UNIQUE,
                ""LaboratoryName"" VARCHAR(255)
            );";
            _context.Database.ExecuteSqlRaw(createTableQuery);

            var createTablesQueryOne = @"
            CREATE TABLE IF NOT EXISTS ""DeploymentInfos"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""RequestedBy"" VARCHAR(255),
                ""To"" VARCHAR(255),  
                ""From"" VARCHAR(255),  
                ""Purpose"" VARCHAR(255),
                ""TodayDate"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                ""DurationDate"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                ""RequestDate"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                ""Laboratory"" VARCHAR(255),
                ""ReleasedBy"" VARCHAR(255),
                ""ReceivedBy"" VARCHAR(255)
            );
            
            CREATE TABLE IF NOT EXISTS ""DeployItems"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""DeploymentInfoId"" INT,
                ""Particular"" VARCHAR(255),
                ""Brand"" VARCHAR(255),
                ""Quantity"" INT,
                ""SerialControlNumber"" VARCHAR(255),
                FOREIGN KEY (""DeploymentInfoId"") REFERENCES ""DeploymentInfos""(""Id"") ON DELETE CASCADE
            );";
            _context.Database.ExecuteSqlRaw(createTablesQueryOne);

            string queryUser = @"
            CREATE TABLE IF NOT EXISTS ""User"" (
                ""UserId"" SERIAL PRIMARY KEY,
                ""FirstName"" VARCHAR(255),
                ""LastName"" VARCHAR(255),
                ""Email"" VARCHAR(255),
                ""Status"" VARCHAR(50),
                ""Role"" VARCHAR(255),
                ""AssignLaboratory"" VARCHAR(255),
                ""Username"" VARCHAR(255),
                ""Password"" VARCHAR(255)
            );";
            _context.Database.ExecuteSqlRaw(queryUser);

            string queryUserTwo = @"
            CREATE TABLE IF NOT EXISTS ""Laboratories"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""LaboratoryName"" VARCHAR(50) NULL,
                ""CreatedDate"" DATE NULL 
            );";
            _context.Database.ExecuteSqlRaw(queryUserTwo);

            string queryUserThree = @"
            CREATE TABLE IF NOT EXISTS ""RepairRequests"" (
                ""Id"" SERIAL PRIMARY KEY,
                ""ItemId"" INT NOT NULL,
                ""TrackNo"" VARCHAR(100) NOT NULL,
                ""CTN"" VARCHAR(100) NULL,
                ""Particular"" VARCHAR(255) NULL,
                ""Brand"" VARCHAR(100) NULL,
                ""SerialStickerNumber"" VARCHAR(100) NULL,
                ""Description"" TEXT NULL,
                ""Status"" VARCHAR(50) NOT NULL,
                ""RequestDate"" TIMESTAMP NOT NULL,
                ""RequestedBy"" VARCHAR(100) NULL,
                ""Location"" VARCHAR(100) NULL,
                ""CompletedDate"" TIMESTAMP NULL,
                ""CompletedBy"" VARCHAR(100) NULL
            );";
            _context.Database.ExecuteSqlRaw(queryUserThree);

            string queryUserFour = @"
            CREATE TABLE IF NOT EXISTS ""Notes"" (
                ""ID"" SERIAL PRIMARY KEY,
                ""RepairRequestId"" INT NOT NULL,
                ""Notes"" VARCHAR(3000) NOT NULL,
                ""CreatedBy"" VARCHAR(100),
                ""CreatedDate"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (""RepairRequestId"") REFERENCES ""RepairRequests""(""Id"")
            );";
            _context.Database.ExecuteSqlRaw(queryUserFour);

            string updateUserTableQuery = @"
            DO $$ 
            BEGIN 
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='User' AND column_name='TemporaryPassword') THEN
                    ALTER TABLE ""User"" ADD COLUMN ""TemporaryPassword"" VARCHAR(255) NULL;
                END IF;

                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='User' AND column_name='TemporaryPasswordExpiry') THEN
                    ALTER TABLE ""User"" ADD COLUMN ""TemporaryPasswordExpiry"" TIMESTAMP NULL;
                END IF;

                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='User' AND column_name='RequiresPasswordChange') THEN
                    ALTER TABLE ""User"" ADD COLUMN ""RequiresPasswordChange"" BOOLEAN DEFAULT FALSE;
                END IF;
            END $$;";
            _context.Database.ExecuteSqlRaw(updateUserTableQuery);
        }

        [HttpPost]
        public IActionResult Index(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Static Admin Credentials
                string UsernameAdmin = "Admin";
                string PasswordAdmin = "Admin";
                string RoleAdmin = "Admin";

                // Admin login check
                if (model.Username == UsernameAdmin && model.Password == PasswordAdmin)
                {
                    HttpContext.Session.SetString("Username", UsernameAdmin);
                    HttpContext.Session.SetString("Role", RoleAdmin);
                    HttpContext.Session.SetString("FullName", "Administrator");

                    return RedirectToAction("Logo", "Home");
                }

                // Fetch user from database
                var user = _context.User.FirstOrDefault(u => u.Username == model.Username);

                if (user != null)
                {
                    bool isValidPassword = false;

                    // Check if using temporary password
                    if (!string.IsNullOrEmpty(user.TemporaryPassword) &&
                        user.TemporaryPasswordExpiry.HasValue &&
                        user.TemporaryPasswordExpiry.Value > DateTime.Now &&
                        user.TemporaryPassword == model.Password)
                    {
                        isValidPassword = true;
                        HttpContext.Session.SetString("RequiresPasswordChange", "true");
                    }
                    // Check regular password
                    else if (user.Password == model.Password)
                    {
                        isValidPassword = true;
                    }

                    if (isValidPassword)
                    {
                        if (string.IsNullOrEmpty(user.Role))
                        {
                            ViewBag.ErrorMessage = "Your account has no role assigned.";
                            return View(model);
                        }

                        HttpContext.Session.SetString("Username", user.Username);
                        HttpContext.Session.SetString("Role", user.Role);
                        HttpContext.Session.SetString("FullName", $"{user.FirstName} {user.LastName}");

                        // If user logged in with temporary password, redirect to password change
                        if (HttpContext.Session.GetString("RequiresPasswordChange") == "true")
                        {
                            return RedirectToAction("ResetPassword");
                        }

                        // Regular login flow
                        if (user.Role.Equals("Technical Assistant", StringComparison.OrdinalIgnoreCase))
                        {
                            var assignedLaboratory = _context.AssignedLaboratories
                                .Where(a => a.LaboratoryName == user.AssignLaboratory)
                                .Select(a => a.LaboratoryName)
                                .FirstOrDefault();

                            if (!string.IsNullOrEmpty(assignedLaboratory))
                            {
                                HttpContext.Session.SetString("AssignedLaboratory", assignedLaboratory);
                                return RedirectToAction("Index", "TALaboratory");
                            }
                            else
                            {
                                ViewBag.ErrorMessage = "No assigned laboratory found.";
                                return View(model);
                            }
                        }
                        else if (user.Role.Equals("Supervisor", StringComparison.OrdinalIgnoreCase))
                        {
                            return RedirectToAction("Logo", "Home");
                        }
                        else
                        {
                            ViewBag.ErrorMessage = "Unauthorized role. Access denied.";
                            return View(model);
                        }
                    }
                    else
                    {
                        ViewBag.ErrorMessage = "Invalid username or password.";
                        return View(model);
                    }
                }
                else
                {
                    ViewBag.ErrorMessage = "Invalid username or password.";
                    return View(model);
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = _context.User
                    .FirstOrDefault(u => u.Username == model.Username && u.Email == model.Email);

                if (user != null)
                {
                    // Generate temporary password
                    string tempPassword = GenerateTemporaryPassword();

                    // Update user with temporary password (expires in 24 hours)
                    user.TemporaryPassword = tempPassword;
                    user.TemporaryPasswordExpiry = DateTime.Now.AddHours(24);
                    user.RequiresPasswordChange = true;

                    await _context.SaveChangesAsync();

                    // Safely send email (handles nulls)
                    bool emailSent = await SendPasswordResetEmailAsync(
                         user.Email ?? string.Empty,
                         $"{user.FirstName ?? string.Empty} {user.LastName ?? string.Empty}".Trim(),
                         tempPassword
                     );

                    if (emailSent)
                    {
                        ViewBag.SuccessMessage = "A temporary password has been sent to your email address.";
                    }
                    else
                    {
                        ViewBag.ErrorMessage = "Failed to send email. Please try again or contact support.";
                    }
                }
                else
                {
                    ViewBag.ErrorMessage = "Username and email combination not found.";
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            if (HttpContext.Session.GetString("RequiresPasswordChange") != "true")
            {
                return RedirectToAction("Index");
            }
            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = _context.User
                    .FirstOrDefault(u => u.Username == model.Username);

                if (user != null && user.TemporaryPassword == model.TemporaryPassword &&
                    user.TemporaryPasswordExpiry.HasValue && user.TemporaryPasswordExpiry.Value > DateTime.Now)
                {
                    // Update password
                    user.Password = model.NewPassword;
                    user.TemporaryPassword = "N/A";
                    user.TemporaryPasswordExpiry = null;
                    user.RequiresPasswordChange = false;

                    _context.SaveChanges();

                    // Clear session flag
                    HttpContext.Session.Remove("RequiresPasswordChange");

                    ViewBag.SuccessMessage = "Password updated successfully. Please login with your new password.";
                    return RedirectToAction("Index");
                }
                else
                {
                    ViewBag.ErrorMessage = "Invalid username or temporary password, or temporary password has expired.";
                }
            }

            return View(model);
        }

        private string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string username, string tempPassword)
        {
            try
            {
                var smtpHost = _config["EmailSettings:SmtpHost"];
                var smtpPort = int.Parse(_config["EmailSettings:SmtpPort"]);
                var smtpUser = _config["EmailSettings:SmtpUser"];
                var smtpPass = _config["EmailSettings:SmtpPass"];
                var fromEmail = _config["EmailSettings:FromEmail"];

                var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true
                };

                var mail = new MailMessage(fromEmail, toEmail)
                {
                    Subject = "Temporary Password",
                    Body = $"Hello {username},\n\nYour temporary password is: {tempPassword}\n\nIt will expire in 24 hours.",
                    IsBodyHtml = false
                };

                await client.SendMailAsync(mail);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ForgotPasswordViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; }

        [Required]
        [Display(Name = "Temporary Password")]
        public string TemporaryPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmNewPassword { get; set; }
    }
}
