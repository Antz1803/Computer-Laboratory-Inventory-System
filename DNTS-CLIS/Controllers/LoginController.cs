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
        // If the table is not exist automatically create
        private void EnsureTrackRecordsTableExists()
        {
            string query = @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TrackRecords')
        BEGIN
            CREATE TABLE TrackRecords (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                TrackNo NVARCHAR(255) NOT NULL,
                ReceiverName NVARCHAR(255) NOT NULL,
                CreatedDate DATETIME NOT NULL
            );
        END";

            _context.Database.ExecuteSqlRaw(query);

            string createTableQuery = @"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AssignedLaboratories')
                BEGIN
                    CREATE TABLE AssignedLaboratories (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        TrackNo NVARCHAR(255) UNIQUE,
                        LaboratoryName NVARCHAR(255),
                    )
                END";

            _context.Database.ExecuteSqlRaw(createTableQuery);

            var createTablesQueryOne = @"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DeploymentInfos')
                BEGIN
                    CREATE TABLE DeploymentInfos (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        RequestedBy NVARCHAR(255),
                        [To] NVARCHAR(255),  
                        [From] NVARCHAR(255),  
                        Purpose NVARCHAR(255),
                        TodayDate DATETIME DEFAULT GETDATE(),
                        DurationDate DATETIME DEFAULT GETDATE(),
                        RequestDate DATETIME DEFAULT GETDATE(),
                        Laboratory NVARCHAR(255),
                        ReleasedBy NVARCHAR(255),
                        ReceivedBy NVARCHAR(255)
                    )
                END;
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DeployItems')
                BEGIN
                    CREATE TABLE DeployItems (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        DeploymentInfoId INT,
                        Particular NVARCHAR(255),
                        Brand NVARCHAR(255),
                        Quantity INT,
                        SerialControlNumber NVARCHAR(255),
                        FOREIGN KEY (DeploymentInfoId) REFERENCES DeploymentInfos(Id) ON DELETE CASCADE
                    )
                END;";
            _context.Database.ExecuteSqlRaw(createTablesQueryOne);

            string queryUser = @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'User')
        BEGIN
            CREATE TABLE [User](
                UserId INT IDENTITY(1,1) PRIMARY KEY,
               FirstName NVARCHAR(255),
                        LastName NVARCHAR(255),
                        Email NVARCHAR(255),
                        Status NVARCHAR(50),
                        Role NVARCHAR(255),
                        AssignLaboratory NVARCHAR(255),
                        Username NVARCHAR(255),
                        Password NVARCHAR(255)
            );
        END";

            _context.Database.ExecuteSqlRaw(queryUser);

            string queryUserTwo = @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Laboratories')
        BEGIN
            CREATE TABLE Laboratories (
    [Id]             INT IDENTITY(1, 1) NOT NULL,
    [LaboratoryName] NVARCHAR(50) NULL,
    [CreatedDate]    DATE NULL );
        END"
            ;

            _context.Database.ExecuteSqlRaw(queryUserTwo);

            string queryUserThree = @"IF NOT EXISTS(SELECT * FROM sys.tables WHERE name = 'RepairRequests')
                BEGIN
                    CREATE TABLE RepairRequests(
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        ItemId INT NOT NULL,
                        TrackNo NVARCHAR(100) NOT NULL,
                        CTN NVARCHAR(100) NULL,
                        Particular NVARCHAR(255) NULL,
                        Brand NVARCHAR(100) NULL,
                        SerialStickerNumber NVARCHAR(100) NULL,
                        Description NVARCHAR(MAX) NULL,
                        Status NVARCHAR(50) NOT NULL,
                        RequestDate DATETIME NOT NULL,
                        RequestedBy NVARCHAR(100) NULL,
                        Location NVARCHAR(100) NULL,
                        CompletedDate DATETIME NULL,
                        CompletedBy NVARCHAR(100) NULL
                    );
                END";
            _context.Database.ExecuteSqlRaw(queryUserThree);

            string queryUserFour = @"IF NOT EXISTS(SELECT * FROM sys.tables WHERE name = 'Notes')
                    BEGIN
                       Create Table dbo.Notes
                    (
                     ID int IDENTITY(1,1) PRIMARY KEY,
                        RepairRequestId int NOT NULL,
                        Notes nvarchar(3000) NOT NULL,
                        CreatedBy nvarchar(100),
                        CreatedDate datetime DEFAULT GETDATE(),
                        FOREIGN KEY (RepairRequestId) REFERENCES RepairRequests(Id)
                    )
                    END";
            _context.Database.ExecuteSqlRaw(queryUserFour);

            string updateUserTableQuery = @"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'User' AND COLUMN_NAME = 'TemporaryPassword')
                BEGIN
                    ALTER TABLE [User] ADD TemporaryPassword NVARCHAR(255) NULL;
                END

                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'User' AND COLUMN_NAME = 'TemporaryPasswordExpiry')
                BEGIN
                    ALTER TABLE [User] ADD TemporaryPasswordExpiry DATETIME NULL;
                END

                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'User' AND COLUMN_NAME = 'RequiresPasswordChange')
                BEGIN
                    ALTER TABLE [User] ADD RequiresPasswordChange BIT DEFAULT 0;
                END";

            _context.Database.ExecuteSqlRaw(updateUserTableQuery);
        }

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
                    bool isTemporaryPassword = false;

                    // Check if using temporary password
                    if (!string.IsNullOrEmpty(user.TemporaryPassword) &&
                        user.TemporaryPasswordExpiry.HasValue &&
                        user.TemporaryPasswordExpiry.Value > DateTime.Now &&
                        user.TemporaryPassword == model.Password)
                    {
                        isValidPassword = true;
                        isTemporaryPassword = true;
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
                    bool emailSent = await _emailService.SendPasswordResetEmailAsync(
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
