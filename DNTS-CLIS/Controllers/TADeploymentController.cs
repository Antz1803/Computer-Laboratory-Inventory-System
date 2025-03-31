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
            ViewBag.Laboratories = laboratories ?? new List<Laboratories>();

            return View();
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
                // Ensure tables exist dynamically
                var createTablesQuery = @"
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

                using var createCmd = new SqlCommand(createTablesQuery, conn, transaction);
                await createCmd.ExecuteNonQueryAsync();

                // Insert into DeploymentInfos
                var insertDeploymentQuery = @"
                INSERT INTO DeploymentInfos (RequestedBy, [To], [From], Purpose, Date, Laboratory, ReleasedBy, ReceivedBy)
                OUTPUT INSERTED.Id
                VALUES (@RequestedBy, @To, @From, @Purpose, @Date, @Laboratory, @ReleasedBy, @ReceivedBy);";

                using var insertCmd = new SqlCommand(insertDeploymentQuery, conn, transaction);
                insertCmd.Parameters.AddWithValue("@RequestedBy", request.RequestedBy ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@To", request.To ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@From", request.From ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Purpose", request.Purpose ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Date", request.Date);
                insertCmd.Parameters.AddWithValue("@Laboratory", request.Laboratory ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@ReleasedBy", request.ReleasedBy ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@ReceivedBy", request.ReceivedBy ?? (object)DBNull.Value);

                int deploymentInfoId = (int)await insertCmd.ExecuteScalarAsync();

                // Insert DeployItems
                var insertItemQuery = @"
                INSERT INTO DeployItems (DeploymentInfoId, Particular, Brand, Quantity, SerialControlNumber)
                VALUES (@DeploymentInfoId, @Particular, @Brand, @Quantity, @SerialControlNumber);";

                foreach (var item in request.DeployItems)
                {
                    using var insertItemCmd = new SqlCommand(insertItemQuery, conn, transaction);
                    insertItemCmd.Parameters.AddWithValue("@DeploymentInfoId", deploymentInfoId);
                    insertItemCmd.Parameters.AddWithValue("@Particular", item.Particular ?? (object)DBNull.Value);
                    insertItemCmd.Parameters.AddWithValue("@Brand", item.Brand ?? (object)DBNull.Value);
                    insertItemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                    insertItemCmd.Parameters.AddWithValue("@SerialControlNumber", item.SerialControlNumber ?? (object)DBNull.Value);
                    await insertItemCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                return Json(new { success = true, message = "Deployment successfully saved!" });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Json(new { success = false, message = $"Error saving deployment: {ex.Message}" });
            }
        }

    }

    public class EquipmentDetails
    {
        public string Particular { get; set; }
        public string Brand { get; set; }
        public string SerialStickerNumber { get; set; }
    }
}