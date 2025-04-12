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
        [HttpPost]
        public IActionResult SaveEditedItem([FromBody] EditedItemModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TrackNo))
            {
                return BadRequest("Invalid data.");
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var query = $@"
                UPDATE [{model.TrackNo}]
                SET CTN = @CTN,
                    Particular = @Particular,
                    Brand = @Brand,
                    [Serial Sticker Number] = @SerialStickerNumber,
                    Status = @Status,
                    Location = @Location
                WHERE Id = @Id";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CTN", model.CTN ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Particular", model.Particular ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Brand", model.Brand ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@SerialStickerNumber", model.SerialStickerNumber ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Status", model.Status ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Location", model.Location ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Id", model.Id);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new { message = "Item updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        public class EditedItemModel
        {
            public int Id { get; set; }
            public string CTN { get; set; }
            public string Particular { get; set; }
            public string Brand { get; set; }
            public string SerialStickerNumber { get; set; }
            public string Status { get; set; }
            public string Location { get; set; }
            public string TrackNo { get; set; }
        }

    }
}
