using System.Data;
using DNTS_CLIS.Data;
using DNTS_CLIS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DNTS_CLIS.Controllers
{
    public class TADeploymentController : Controller
    {
        private readonly DNTS_CLISContext _context;

        public TADeploymentController(DNTS_CLISContext context)
        {
            _context = context;
        }


        public async Task<IActionResult> TAHistoryDeployment()
        {
            var historyList = await _context.DeploymentInfos.ToListAsync();
            return View(historyList);
        }


        public async Task<IActionResult> PreviewDeployment(int id)
        {
            var deploymentInfo = await _context.DeploymentInfos
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deploymentInfo == null)
            {
                return NotFound();
            }

            var deployItems = await _context.DeployItems
                .Where(d => d.DeploymentInfoId == id)
                .ToListAsync();

            var viewModel = new DeploymentPreviewViewModel
            {
                DeploymentInfos = deploymentInfo,
                DeployItems = deployItems
            };

            return View(viewModel);
        }

        public async Task<IActionResult> DeleteDeployment(int id)
        {
            var deployment = await _context.DeploymentInfos.FindAsync(id);
            if (deployment != null)
            {
                _context.DeploymentInfos.Remove(deployment);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("TAHistoryDeployment");
        }
        public async Task<IActionResult> Index()
        {
            var laboratories = await _context.Laboratories.ToListAsync();

            var sortedLaboratories = laboratories
                .OrderBy(l => ExtractLabNumber(l.LaboratoryName))
                .ThenBy(l => l.LaboratoryName)
                .ToList();

            ViewBag.Laboratories = sortedLaboratories;

            return View();
        }

        private int ExtractLabNumber(string labName)
        {
            var match = System.Text.RegularExpressions.Regex.Match(labName ?? "", @"\d+");
            return match.Success ? int.Parse(match.Value) : int.MaxValue;
        }

        [HttpGet]
        public async Task<JsonResult> GetCTNsByAssignedLaboratory(string laboratoryName)
        {
            if (string.IsNullOrEmpty(laboratoryName))
                return Json(new { success = false, message = "Laboratory Name is required." });

            try
            {
                // Get all track numbers assigned to the selected laboratory, ordered
                var trackNos = await _context.AssignedLaboratories
                    .Where(l => l.LaboratoryName == laboratoryName)
                    .Select(l => l.TrackNo)
                    .Distinct()
                    .OrderBy(t => t)
                    .ToListAsync();

                if (!trackNos.Any())
                    return Json(new { success = false, message = "No Track Numbers found for this Laboratory." });

                var ctnList = new HashSet<string>(); // HashSet to get unique CTNs

                foreach (var trackNo in trackNos)
                {
                    string tableName = trackNo;
                    string query = $"SELECT DISTINCT CTN FROM [{tableName}]";

                    try
                    {
                        var records = await _context.Database
                            .SqlQueryRaw<string>(query)
                            .ToListAsync();

                        ctnList.UnionWith(records);
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = $"Error querying {tableName}: {ex.Message}" });
                    }
                }

                // Convert to ordered list with numeric sorting
                var orderedCtnList = ctnList
                    .Select(ctn => int.TryParse(ctn, out int num) ? num : int.MaxValue)
                    .OrderBy(x => x)
                    .Select(num => num.ToString())
                    .ToList();

                return Json(new { success = true, data = orderedCtnList });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error fetching CTNs: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetEquipmentDetails(string ctn, string laboratoryName)
        {
            if (string.IsNullOrEmpty(ctn) || string.IsNullOrEmpty(laboratoryName))
                return Json(new { success = false, message = "CTN and Laboratory Name are required." });

            try
            {
                var trackNos = await _context.AssignedLaboratories
                    .Where(l => l.LaboratoryName == laboratoryName)
                    .Select(l => l.TrackNo)
                    .Distinct()
                    .ToListAsync();

                if (!trackNos.Any())
                    return Json(new { success = false, message = "No assigned laboratories found." });

                var equipmentList = new List<EquipmentDetails>();
                var connectionString = _context.Database.GetConnectionString();

                foreach (var trackNo in trackNos)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(trackNo))
                            continue;

                        using (var connection = new SqlConnection(connectionString))
                        {
                            await connection.OpenAsync();

                            // Find the exact column name for serial number
                            string columnQuery = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{trackNo}' AND COLUMN_NAME LIKE '%SERIAL%'";
                            string serialColumn = null;

                            using (var cmd = new SqlCommand(columnQuery, connection))
                            {
                                var result = await cmd.ExecuteScalarAsync();
                                serialColumn = result?.ToString();
                            }

                            if (serialColumn == null)
                            {
                                continue;
                            }

                            string query = $@"
                            SELECT 
                                Particular, 
                                Brand, 
                                [{serialColumn}] AS SerialStickerNumber 
                            FROM [{trackNo}] 
                            WHERE CTN = @ctn";

                            using (var command = new SqlCommand(query, connection))
                            {
                                command.Parameters.Add(new SqlParameter("@ctn", SqlDbType.NVarChar) { Value = ctn });

                                using (var reader = await command.ExecuteReaderAsync())
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        equipmentList.Add(new EquipmentDetails
                                        {
                                            Particular = reader["Particular"] == DBNull.Value ? null : reader["Particular"].ToString(),
                                            Brand = reader["Brand"] == DBNull.Value ? null : reader["Brand"].ToString(),
                                            SerialStickerNumber = reader["SerialStickerNumber"] == DBNull.Value ? null : reader["SerialStickerNumber"].ToString()
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error querying {trackNo}: {ex.Message}");
                    }
                }

                if (!equipmentList.Any())
                    return Json(new { success = false, message = "No equipment found for the selected CTN." });

                return Json(new { success = true, data = equipmentList });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error fetching equipment details: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitDeploymentForm([FromBody] DeploymentInfo request)
        {
            if (request == null || request.DeployItems == null || !request.DeployItems.Any())
                return Json(new { success = false, message = "Invalid submission. No equipment data received." });

            using var conn = new SqlConnection(_context.Database.GetConnectionString());
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            try
            {
                // Ensure DeploymentInfos table exists
                var createDeploymentInfosQuery = @"
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
                Laboratory NVARCHAR(255),
                ReleasedBy NVARCHAR(255),
                ReceivedBy NVARCHAR(255)
            )
        END";

                // Ensure DeployItems table exists
                var createDeployItemsQuery = @"
        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DeployItems')
        BEGIN
            CREATE TABLE DeployItems (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                DeploymentInfoId INT FOREIGN KEY REFERENCES DeploymentInfos(Id),
                Particular NVARCHAR(255),
                Brand NVARCHAR(255),
                Quantity INT,
                SerialControlNumber NVARCHAR(255)
            )
        END";

                using (var cmd = new SqlCommand(createDeploymentInfosQuery, conn, transaction))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = new SqlCommand(createDeployItemsQuery, conn, transaction))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert into DeploymentInfos
                var insertDeploymentInfoQuery = @"
        INSERT INTO DeploymentInfos (RequestedBy, [To], [From], Purpose, TodayDate, DurationDate, Laboratory, ReleasedBy, ReceivedBy)
        OUTPUT INSERTED.Id
        VALUES (@RequestedBy, @To, @From, @Purpose, @TodayDate, @DurationDate, @Laboratory, @ReleasedBy, @ReceivedBy)";

                int deploymentInfoId;
                using (var cmd = new SqlCommand(insertDeploymentInfoQuery, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@RequestedBy", request.RequestedBy ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@To", request.To ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@From", request.From ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Purpose", request.Purpose ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@TodayDate", request.TodayDate);
                    cmd.Parameters.AddWithValue("@DurationDate", request.DurationDate);
                    cmd.Parameters.AddWithValue("@Laboratory", request.Laboratory ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReleasedBy", request.ReleasedBy ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReceivedBy", request.ReceivedBy ?? (object)DBNull.Value);

                    deploymentInfoId = (int)await cmd.ExecuteScalarAsync();
                }

                // Insert related DeployItems
                foreach (var item in request.DeployItems)
                {
                    var insertDeployItemQuery = @"
            INSERT INTO DeployItems (DeploymentInfoId, Particular, Brand, Quantity, SerialControlNumber)
            VALUES (@DeploymentInfoId, @Particular, @Brand, @Quantity, @SerialControlNumber)";

                    using (var cmd = new SqlCommand(insertDeployItemQuery, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@DeploymentInfoId", deploymentInfoId);
                        cmd.Parameters.AddWithValue("@Particular", item.Particular ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Brand", item.Brand ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                        cmd.Parameters.AddWithValue("@SerialControlNumber", item.SerialControlNumber ?? (object)DBNull.Value);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                // Commit transaction
                await UpdateEquipmentLocation(request, conn, transaction);
                transaction.Commit();

                return Json(new { success = true, message = "Deployment form submitted successfully." });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Json(new { success = false, message = $"Error submitting deployment form: {ex.Message}" });
            }
        }

        private async Task UpdateEquipmentLocation(DeploymentInfo request, SqlConnection conn, SqlTransaction transaction)
        {
            try
            {
                // Get track numbers for the selected laboratory
                var trackNos = await _context.AssignedLaboratories
                    .Where(l => l.LaboratoryName == request.Laboratory)
                    .Select(l => l.TrackNo)
                    .Distinct()
                    .ToListAsync();

                foreach (var trackNo in trackNos)
                {
                    if (string.IsNullOrWhiteSpace(trackNo))
                        continue;

                    // Check if MovedTo column exists in the track table
                    string checkColumnQuery = $@"
                        SELECT COUNT(*) 
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = '{trackNo}' AND COLUMN_NAME = 'MOVEDTO'";

                    using var checkCmd = new SqlCommand(checkColumnQuery, conn, transaction);
                    int columnExists = (int)await checkCmd.ExecuteScalarAsync();

                    if (columnExists == 0)
                    {
                        // Add MovedTo column if it doesn't exist
                        string addColumnQuery = $"ALTER TABLE [{trackNo}] ADD MOVEDTO NVARCHAR(255)";
                        using var addColCmd = new SqlCommand(addColumnQuery, conn, transaction);
                        await addColCmd.ExecuteNonQueryAsync();
                    }

                    // Find the serial number column name
                    string serialColumnQuery = $@"
                        SELECT COLUMN_NAME 
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = '{trackNo}' AND COLUMN_NAME LIKE '%SERIAL%'";

                    using var serialCmd = new SqlCommand(serialColumnQuery, conn, transaction);
                    string serialColumn = (await serialCmd.ExecuteScalarAsync())?.ToString();

                    if (string.IsNullOrEmpty(serialColumn))
                        continue;

                    // Update MovedTo for each deployed item
                    foreach (var item in request.DeployItems)
                    {
                        if (!string.IsNullOrEmpty(item.SerialControlNumber))
                        {
                            string updateLocationQuery = $@"
                                UPDATE [{trackNo}] 
                                SET MOVEDTO = @MovedTo 
                                WHERE [{serialColumn}] = @SerialNumber";

                            using var updateCmd = new SqlCommand(updateLocationQuery, conn, transaction);
                            updateCmd.Parameters.AddWithValue("@MovedTo", request.To ?? (object)DBNull.Value);
                            updateCmd.Parameters.AddWithValue("@SerialNumber", item.SerialControlNumber);

                            await updateCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating equipment location: {ex.Message}");
                throw;
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetDueDateNotifications()
        {
            var currentDate = DateTime.Now.Date;
            var count = await _context.DeploymentInfos
                .Where(d => d.DurationDate.Date <= currentDate.AddDays(3))
                .CountAsync();

            return Json(new { count = count });
        }

        [HttpGet]
        public async Task<IActionResult> GetDueDateDetails()
        {
            var currentDate = DateTime.Now.Date;
            var deployments = await _context.DeploymentInfos
      .Include(d => d.DeployItems)
      .Where(d => d.DurationDate.Date <= currentDate.AddDays(3))
      .ToListAsync();

            var notifications = deployments.Select(d => new {
                id = d.Id,
                laboratory = d.Laboratory,
                durationDate = d.DurationDate,
                type = "deployment",
                deployItems = d.DeployItems.Select(di => new {
                    id = di.Id,
                    particular = di.Particular,
                    serialcontrolnumber = di.SerialControlNumber
                }).ToList()
            }).ToList();

            return Json(new { notifications = notifications });
        }

    }

    public class EquipmentDetails
    {
        public string Particular { get; set; }
        public string Brand { get; set; }
        public string SerialStickerNumber { get; set; }
    }
}