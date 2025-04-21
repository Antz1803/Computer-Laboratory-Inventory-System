using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static DNTS_CLIS.Controllers.TALaboratoryController;

namespace DNTS_CLIS.Controllers
{
    public class RepairRequestsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public RepairRequestsController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DNTS_CLISContext");
        }
        public IActionResult Index()
        {
            // Check if user is authorized
            string username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrWhiteSpace(username))
            {
                return RedirectToAction("Index", "Login");
            }

            // Get all pending and repairing repair requests
            var pendingRequests = GetPendingRepairRequests();

            return View(pendingRequests);
        }

        private List<RepairRequestModel> GetPendingRepairRequests()
        {
            var requests = new List<RepairRequestModel>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string query = @"
                    SELECT * FROM RepairRequests
                    WHERE Status IN ('Pending', 'Repairing')
                    ORDER BY RequestDate DESC";

                using (var cmd = new SqlCommand(query, conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            requests.Add(new RepairRequestModel
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                                TrackNo = reader.GetString(reader.GetOrdinal("TrackNo")),
                                CTN = reader["CTN"] as string,
                                Particular = reader["Particular"] as string,
                                Brand = reader["Brand"] as string,
                                SerialStickerNumber = reader["SerialStickerNumber"] as string,
                                Description = reader["Description"] as string,
                                Status = reader["Status"] as string,
                                RequestDate = reader.GetDateTime(reader.GetOrdinal("RequestDate")),
                                RequestedBy = reader["RequestedBy"] as string,
                                Location = reader["Location"] as string
                            });
                        }
                    }
                }
            }

            return requests;
        }

        [HttpGet]
        public JsonResult GetRepairRequest(int id)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string query = "SELECT * FROM RepairRequests WHERE Id = @Id";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var request = new
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    ItemId = reader.GetInt32(reader.GetOrdinal("ItemId")),
                                    TrackNo = reader["TrackNo"] as string,
                                    CTN = reader["CTN"] as string,
                                    Particular = reader["Particular"] as string,
                                    Brand = reader["Brand"] as string,
                                    SerialStickerNumber = reader["SerialStickerNumber"] as string,
                                    Description = reader["Description"] as string,
                                    Status = reader["Status"] as string,
                                    RequestDate = reader.GetDateTime(reader.GetOrdinal("RequestDate")),
                                    RequestedBy = reader["RequestedBy"] as string,
                                    Location = reader["Location"] as string
                                };

                                return Json(request);
                            }
                        }
                    }

                    Response.StatusCode = 404;
                    return Json(new { error = "Repair request not found" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        [Route("RepairRequests/ProcessRepairRequest")]
        public JsonResult ProcessRepairRequest([FromBody] RepairRequestActionModel model)
        {
            try
            {
                if (model == null)
                {
                    return Json(new { success = false, message = "Invalid request data." });
                }

                int id = model.id;
                string action = model.action?.ToLower() ?? string.Empty;

                System.Diagnostics.Debug.WriteLine($"Processing repair request - ID: {id}, Action: {action}");

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string getRequestQuery = @"SELECT TrackNo, ItemId, Status FROM RepairRequests WHERE Id = @Id";
                    string trackNo = "";
                    int itemId = 0;
                    string currentStatus = "";

                    using (var cmd = new SqlCommand(getRequestQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                trackNo = reader["TrackNo"].ToString();
                                itemId = Convert.ToInt32(reader["ItemId"]);
                                currentStatus = reader["Status"].ToString();
                            }
                            else
                            {
                                return Json(new { success = false, message = "Repair request not found." });
                            }
                        }
                    }

                    // Define status updates for both tables
                    string repairRequestsStatus = "";
                    string talabStatus = "";

                    if (action == "accept")
                    {
                        repairRequestsStatus = "Repairing";
                        talabStatus = "REPAIRING"; 

                        // Only allow this action if current status is Pending
                        if (currentStatus != "Pending")
                        {
                            return Json(new { success = false, message = "Can only accept requests with 'Pending' status." });
                        }
                    }
                    else if (action == "decline")
                    {
                        repairRequestsStatus = "Declined";
                        talabStatus = "FUNCTIONAL"; 

                        // Only allow this action if current status is Pending
                        if (currentStatus != "Pending")
                        {
                            return Json(new { success = false, message = "Can only decline requests with 'Pending' status." });
                        }
                    }
                    else if (action == "complete")
                    {
                        repairRequestsStatus = "Completed";
                        talabStatus = "FUNCTIONAL"; 

                        // Only allow this action if current status is Repairing
                        if (currentStatus != "Repairing")
                        {
                            return Json(new { success = false, message = "Can only complete requests with 'Repairing' status." });
                        }
                    }
                    else
                    {
                        return Json(new { success = false, message = $"Invalid action: '{action}'." });
                    }

                    // Begin transaction to ensure both updates succeed or fail together
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // Update status in RepairRequests table
                            string updateRepairRequestQuery = @"UPDATE RepairRequests 
                                                       SET Status = @Status 
                                                       WHERE Id = @Id";

                            using (var cmd = new SqlCommand(updateRepairRequestQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Status", repairRequestsStatus);
                                cmd.Parameters.AddWithValue("@Id", id);
                                int rowsAffected = cmd.ExecuteNonQuery();

                                if (rowsAffected == 0)
                                {
                                    transaction.Rollback();
                                    return Json(new { success = false, message = $"Failed to update repair request status." });
                                }
                            }

                            // Update status in the original table (e.g., TrackNo_1665_MONITOR_TA)
                            string safeTableName = trackNo.Replace("'", "''");

                            // Check if STATUS column exists in this table
                            bool statusColumnExists = false;
                            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{safeTableName}' AND COLUMN_NAME = 'STATUS'", conn, transaction))
                            {
                                int columnCount = (int)cmd.ExecuteScalar();
                                statusColumnExists = columnCount > 0;
                            }

                            if (statusColumnExists)
                            {
                                string updateOriginalTableQuery = $@"UPDATE [{safeTableName}]
                                                          SET STATUS = @Status
                                                          WHERE ID = @ItemId";

                                using (var cmd = new SqlCommand(updateOriginalTableQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Status", talabStatus);
                                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();

                            return Json(new
                            {
                                success = true,
                                message = $"Repair request successfully {action}ed. Status updated in both tables."
                            });
                        }
                        catch (Exception ex)
                        {
                            // Roll back transaction if there's an error
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"Transaction error: {ex.Message}");
                            return Json(new { success = false, message = $"Error updating status: {ex.Message}" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ProcessRepairRequest: {ex.Message}");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        public class RepairRequestActionModel
        {
            public int id { get; set; }
            public string action { get; set; }
        }
    }
}

