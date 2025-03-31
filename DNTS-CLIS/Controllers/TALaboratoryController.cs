using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
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
            string assignedLab = HttpContext.Session.GetString("AssignedLaboratory");
            if (string.IsNullOrEmpty(assignedLab))
            {
                return RedirectToAction("Index", "Login");
            }

            string tableName = GetTrackNoFromLab(assignedLab); 

            if (string.IsNullOrEmpty(tableName))
            {
                ViewBag.ErrorMessage = "No associated table found for this laboratory.";
                return View();
            }

            DataTable dt = new DataTable();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                try
                {
                    conn.Open();
                    string query = $"SELECT * FROM [{tableName}]"; 

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        da.Fill(dt);
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.ErrorMessage = "Error loading laboratory data: " + ex.Message;
                }
            }

            ViewBag.LaboratoryName = assignedLab;
            ViewBag.DataTable = dt;

            return View();
        }

        // Fetch TrackNo based on AssignedLaboratory name
        private string GetTrackNoFromLab(string laboratoryName)
        {
            string trackNo = null;
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = "SELECT TrackNo FROM AssignedLaboratories WHERE LaboratoryName = @LaboratoryName";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@LaboratoryName", laboratoryName);
                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        trackNo = result.ToString();
                    }
                }
            }
            return trackNo;
        }
    }
}
