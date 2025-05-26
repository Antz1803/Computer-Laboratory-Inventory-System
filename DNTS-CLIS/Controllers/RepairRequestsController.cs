using System.Data;
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

                    // Define status updates
                    string repairRequestsStatus = "";
                    string talabStatus = "";

                    if (action == "accept")
                    {
                        repairRequestsStatus = "Repairing";
                        talabStatus = "REPAIRING";

                        if (currentStatus != "Pending")
                        {
                            return Json(new { success = false, message = "Can only accept requests with 'Pending' status." });
                        }
                    }
                    else if (action == "incomplete")
                    {
                        repairRequestsStatus = "Pending Validation";
                        talabStatus = "REPAIRING"; // Keep as repairing until supervisor validates

                        if (currentStatus != "Repairing")
                        {
                            return Json(new { success = false, message = "Can only mark as incomplete if status is 'Repairing'." });
                        }

                        // Create notification for supervisor
                        CreateSupervisorNotification(conn, id);
                    }
                    else if (action == "complete")
                    {
                        repairRequestsStatus = "Completed";
                        talabStatus = "FUNCTIONAL";

                        if (currentStatus != "Repairing")
                        {
                            return Json(new { success = false, message = "Can only complete requests with 'Repairing' status." });
                        }
                    }
                    else
                    {
                        return Json(new { success = false, message = $"Invalid action: '{action}'." });
                    }

                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
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

                            // Only update the original table status if not pending validation
                            if (action != "incomplete")
                            {
                                string safeTableName = trackNo.Replace("'", "''");

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
                            }

                            transaction.Commit();

                            string statusMessage = action == "incomplete"
                                ? "marked as incomplete and sent for supervisor validation"
                                : $"successfully {action}ed";

                            return Json(new
                            {
                                success = true,
                                message = $"Repair request {statusMessage}."
                            });
                        }
                        catch (Exception ex)
                        {
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

        private void CreateSupervisorNotification(SqlConnection conn, int repairRequestId)
        {
            try
            {
                // Create notifications table if it doesn't exist
                string createTableQuery = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SupervisorNotifications' AND xtype='U')
                    BEGIN
                        CREATE TABLE SupervisorNotifications (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            RepairRequestId INT NOT NULL,
                            Message NVARCHAR(500) NOT NULL,
                            IsRead BIT DEFAULT 0,
                            CreatedDate DATETIME DEFAULT GETDATE(),
                            FOREIGN KEY (RepairRequestId) REFERENCES RepairRequests(Id)
                        )
                    END";

                using (var cmd = new SqlCommand(createTableQuery, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // Insert notification
                string insertNotificationQuery = @"
                    INSERT INTO SupervisorNotifications (RepairRequestId, Message, IsRead, CreatedDate)
                    VALUES (@RepairRequestId, @Message, 0, GETDATE())";

                using (var cmd = new SqlCommand(insertNotificationQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@RepairRequestId", repairRequestId);
                    cmd.Parameters.AddWithValue("@Message", "Repair request marked as incomplete and requires validation");
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating supervisor notification: {ex.Message}");
            }
        }

        [HttpGet]
        public JsonResult GetNotificationCount()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT COUNT(*) 
                        FROM SupervisorNotifications sn
                        INNER JOIN RepairRequests rr ON sn.RepairRequestId = rr.Id
                        WHERE sn.IsRead = 0 AND rr.Status = 'Pending Validation'";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        int count = (int)cmd.ExecuteScalar();
                        return Json(new { count = count });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting notification count: {ex.Message}");
                return Json(new { count = 0 });
            }
        }

        [HttpGet]
        public JsonResult GetNotifications()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            sn.Id,
                            sn.RepairRequestId,
                            sn.Message,
                            sn.CreatedDate,
                            rr.TrackNo,
                            rr.Particular,
                            rr.Brand,
                            rr.RequestDate,
                            rr.RequestedBy
                        FROM SupervisorNotifications sn
                        INNER JOIN RepairRequests rr ON sn.RepairRequestId = rr.Id
                        WHERE sn.IsRead = 0 AND rr.Status = 'Pending Validation'
                        ORDER BY sn.CreatedDate DESC";

                    var notifications = new List<object>();

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                notifications.Add(new
                                {
                                    id = reader.GetInt32("Id"),
                                    repairRequestId = reader.GetInt32("RepairRequestId"),
                                    message = reader["Message"].ToString(),
                                    createdDate = reader.GetDateTime("CreatedDate"),
                                    trackNo = reader["TrackNo"].ToString(),
                                    particular = reader["Particular"]?.ToString() ?? "",
                                    brand = reader["Brand"]?.ToString() ?? "",
                                    requestDate = reader.GetDateTime("RequestDate"),
                                    requestedBy = reader["RequestedBy"]?.ToString() ?? ""
                                });
                            }
                        }
                    }

                    return Json(new { notifications = notifications });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting notifications: {ex.Message}");
                return Json(new { notifications = new List<object>() });
            }
        }

        [HttpPost]
        public JsonResult ValidateDefective([FromBody] ValidateDefectiveModel model)
        {
            try
            {
                if (model == null)
                {
                    return Json(new { success = false, message = "Invalid request data." });
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Get repair request details
                    string getRequestQuery = @"
                        SELECT rr.Id, rr.TrackNo, rr.ItemId, sn.Id as NotificationId
                        FROM RepairRequests rr
                        INNER JOIN SupervisorNotifications sn ON rr.Id = sn.RepairRequestId
                        WHERE sn.Id = @NotificationId AND rr.Status = 'Pending Validation'";

                    int repairRequestId = 0;
                    string trackNo = "";
                    int itemId = 0;

                    using (var cmd = new SqlCommand(getRequestQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@NotificationId", model.notificationId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                repairRequestId = reader.GetInt32("Id");
                                trackNo = reader["TrackNo"].ToString();
                                itemId = reader.GetInt32("ItemId");
                            }
                            else
                            {
                                return Json(new { success = false, message = "Notification or repair request not found." });
                            }
                        }
                    }

                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string newStatus = "";
                            string newTalabStatus = "";
                            string successMessage = "";

                            if (model.approve)
                            {
                                // Approve as defective
                                newStatus = "Incompleted";
                                newTalabStatus = "DEFECTIVE";
                                successMessage = "Item validated as defective successfully.";
                            }
                            else
                            {
                                // Return to repairing
                                newStatus = "Repairing";
                                newTalabStatus = "REPAIRING";
                                successMessage = "Item returned to repairing status successfully.";
                            }

                            // Update repair request status
                            string updateRepairRequestQuery = @"
                                UPDATE RepairRequests 
                                SET Status = @Status 
                                WHERE Id = @Id";

                            using (var cmd = new SqlCommand(updateRepairRequestQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Status", newStatus);
                                cmd.Parameters.AddWithValue("@Id", repairRequestId);
                                cmd.ExecuteNonQuery();
                            }

                            // Update original table status
                            string safeTableName = trackNo.Replace("'", "''");

                            bool statusColumnExists = false;
                            using (var cmd = new SqlCommand($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{safeTableName}' AND COLUMN_NAME = 'STATUS'", conn, transaction))
                            {
                                int columnCount = (int)cmd.ExecuteScalar();
                                statusColumnExists = columnCount > 0;
                            }

                            if (statusColumnExists)
                            {
                                string updateOriginalTableQuery = $@"
                                    UPDATE [{safeTableName}]
                                    SET STATUS = @Status
                                    WHERE ID = @ItemId";

                                using (var cmd = new SqlCommand(updateOriginalTableQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Status", newTalabStatus);
                                    cmd.Parameters.AddWithValue("@ItemId", itemId);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // Mark notification as read
                            string markNotificationReadQuery = @"
                                UPDATE SupervisorNotifications 
                                SET IsRead = 1 
                                WHERE Id = @NotificationId";

                            using (var cmd = new SqlCommand(markNotificationReadQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@NotificationId", model.notificationId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            return Json(new
                            {
                                success = true,
                                message = successMessage
                            });
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine($"Transaction error: {ex.Message}");
                            return Json(new { success = false, message = $"Error processing validation: {ex.Message}" });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ValidateDefective: {ex.Message}");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult MarkAllNotificationsAsRead()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string query = @"
                        UPDATE SupervisorNotifications 
                        SET IsRead = 1 
                        WHERE IsRead = 0";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        int rowsAffected = cmd.ExecuteNonQuery();
                        return Json(new { success = true, message = $"{rowsAffected} notifications marked as read." });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error marking notifications as read: {ex.Message}");
                return Json(new { success = false, message = "Error marking notifications as read: " + ex.Message });
            }
        }

        public class RepairRequestActionModel
        {
            public int id { get; set; }
            public string action { get; set; }
        }

        public class ValidateDefectiveModel
        {
            public int notificationId { get; set; }
            public bool approve { get; set; }
        }
    }
}