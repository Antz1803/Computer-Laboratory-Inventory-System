using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using DNTS_CLIS.Data;

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
            // Get the assigned laboratory for the logged-in user
            string assignedLab = HttpContext.Session.GetString("AssignedLaboratory");
            if (string.IsNullOrEmpty(assignedLab))
            {
                return RedirectToAction("Index", "Login");
            }

            ViewBag.LaboratoryName = assignedLab;
            ViewBag.TrackNos = GetTrackNosForLaboratory(assignedLab);

            return View();
        }

        // Fetch TrackNos based on the laboratory name
        private List<string> GetTrackNosForLaboratory(string laboratoryName)
        {
            var tracknos = new List<string>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = "SELECT DISTINCT TrackNo FROM AssignedLaboratories WHERE LaboratoryName = @LaboratoryName";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@LaboratoryName", laboratoryName);
                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        tracknos.Add(reader.GetString(0));
                    }
                }
            }
            return tracknos;
        }

        [HttpGet]
        public JsonResult GetTableDataOne(string trackNo)
        {
            var dataTable = new DataTable();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                using (SqlDataAdapter adapter = new SqlDataAdapter($"SELECT * FROM [{trackNo}]", conn))
                {
                    adapter.Fill(dataTable);
                }
            }

            var result = new List<Dictionary<string, object>>();
            foreach (DataRow row in dataTable.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in dataTable.Columns)
                {
                    dict[col.ColumnName] = row[col] ?? DBNull.Value;
                }
                result.Add(dict);
            }

            return Json(result);
        }

        [HttpPost]
        public IActionResult RepairItem(string trackNo, int id, string description)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string query = $"UPDATE [{trackNo}] SET RepairDescription = @Description WHERE ID = @ID";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ID", id);
                    cmd.Parameters.AddWithValue("@Description", description);
                    cmd.ExecuteNonQuery();
                }
            }

            return Ok("Item marked for repair.");
        }

        [HttpPost]
        public IActionResult EditItem(string trackNo, int id, [FromBody] Dictionary<string, object> data)

        {
            if (string.IsNullOrEmpty(trackNo) || id <= 0 || data == null || data.Count == 0)
            {
                return BadRequest("Invalid request data.");
            }

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var pair in data)
                        {
                            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value != null)
                            {
                                string query = $"UPDATE [{trackNo}] SET {pair.Key} = @Value WHERE ID = @ID";
                                using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@ID", id);
                                    cmd.Parameters.AddWithValue("@Value", pair.Value);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return Ok(new { message = "Item updated successfully." });
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return StatusCode(500, new { error = $"Failed to update item: {ex.Message}" });
                    }
                }
            }
        }
    }
}
