using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DNTS_CLIS.Controllers
{
    public class TALaboratoryController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public TALaboratoryController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DNTS_CLISContext");
        }

        public IActionResult Index()
        {
            string assignedLab = HttpContext.Session.GetString("AssignedLaboratory");
            if (string.IsNullOrWhiteSpace(assignedLab))
            {
                return RedirectToAction("Index", "Login");
            }

            ViewBag.LaboratoryName = assignedLab;
            ViewBag.TrackNos = GetTrackNosForLaboratory(assignedLab);

            return View();
        }

        private List<string> GetTrackNosForLaboratory(string laboratoryName)
        {
            var trackNos = new List<string>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var query = "SELECT DISTINCT TrackNo FROM AssignedLaboratories WHERE LaboratoryName = @LaboratoryName";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@LaboratoryName", laboratoryName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            trackNos.Add(reader.GetString(0));
                        }
                    }
                }
            }

            return trackNos;
        }

        [HttpGet]
        public JsonResult GetTableDataOne(string trackNo)
        {
            if (string.IsNullOrWhiteSpace(trackNo))
            {
                return Json(new { error = "TrackNo is required." });
            }

            var dataTable = new DataTable();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var query = $"SELECT * FROM [{trackNo}]";

                using (var adapter = new SqlDataAdapter(query, conn))
                {
                    adapter.Fill(dataTable);
                }
            }

            var result = dataTable.AsEnumerable()
                .Select(row => dataTable.Columns.Cast<DataColumn>()
                .ToDictionary(col => col.ColumnName, col => row[col] ?? DBNull.Value))
                .ToList();

            return Json(result);
        }
        // This model class should match the JSON being sent from the client
        public class EditedItemModel
        {
            public int Id { get; set; }
            public string TrackNo { get; set; }
            public string CTN { get; set; }
            public string Particular { get; set; }
            public string Brand { get; set; }
            public string SerialStickerNumber { get; set; }
            public string Status { get; set; }
            public string Location { get; set; }
        }

        [HttpPost]
        public IActionResult SaveEditedItem([FromBody] EditedItemModel model)
        {
            if (model == null)
            {
                return BadRequest("Model is null");
            }

            if (string.IsNullOrWhiteSpace(model.TrackNo))
            {
                return BadRequest("TrackNo is required");
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Log what we're trying to do
                    System.Diagnostics.Debug.WriteLine($"Updating item {model.Id} in table {model.TrackNo}");

                    // Safely escape the table name
                    string safeTableName = model.TrackNo.Replace("'", "''");

                    // Create the SQL query - note how we're handling the column name with spaces
                    string query = $@"
                UPDATE [{safeTableName}]
                SET 
                    CTN = @CTN,
                    Particular = @Particular,
                    Brand = @Brand,
                    [SERIALSTICKERNUMBER] = @SerialStickerNumber,
                    Status = @Status,
                    Location = @Location
                WHERE Id = @Id";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        // Add parameters
                        cmd.Parameters.AddWithValue("@CTN", (object)model.CTN ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Particular", (object)model.Particular ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Brand", (object)model.Brand ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@SerialStickerNumber", (object)model.SerialStickerNumber ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Status", (object)model.Status ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Location", (object)model.Location ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@Id", model.Id);

                        // Execute and get affected rows
                        int rowsAffected = cmd.ExecuteNonQuery();

                        // Log the result
                        System.Diagnostics.Debug.WriteLine($"Rows affected: {rowsAffected}");

                        if (rowsAffected == 0)
                        {
                            return NotFound($"No record found with ID {model.Id} in table {model.TrackNo}");
                        }
                    }

                    return Ok(new { success = true, message = "Item updated successfully" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveEditedItem: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                return StatusCode(500, new { success = false, message = $"Internal server error: {ex.Message}" });
            }
        }
    }
}
