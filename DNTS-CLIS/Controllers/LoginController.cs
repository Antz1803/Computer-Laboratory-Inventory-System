using DNTS_CLIS.Data;
using DNTS_CLIS.Models;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NuGet.DependencyResolver;
using System.Drawing.Drawing2D;
using System.Security.Principal;

namespace DNTS_CLIS.Controllers
{
    public class LoginController : Controller
    {
        private readonly DNTS_CLISContext _context;

        public LoginController(DNTS_CLISContext context)
        {
            _context = context;
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
                        Date DATETIME DEFAULT GETDATE(),
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
                var user = _context.User
                    .FirstOrDefault(u => u.Username == model.Username && u.Password == model.Password);

                if (user != null)
                {
                    if (string.IsNullOrEmpty(user.Role))
                    {
                        ViewBag.ErrorMessage = "Your account has no role assigned.";
                        return View(model);
                    }

                    HttpContext.Session.SetString("Username", user.Username);
                    HttpContext.Session.SetString("Role", user.Role);
                    HttpContext.Session.SetString("FullName", $"{user.FirstName} {user.LastName}");

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

            return View(model);
        }

    }
}
