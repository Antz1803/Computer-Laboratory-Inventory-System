using System.ComponentModel;
using System.Data;
using System.Text.RegularExpressions;
using DNTS_CLIS.Data;
using DNTS_CLIS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OfficeOpenXml;

namespace DNTS_CLIS.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly DNTS_CLISContext _context;

        public HomeController(IConfiguration configuration, DNTS_CLISContext context)
        {
            _configuration = configuration;
            _context = context;
        }
        public IActionResult AddingLaboratories()
        {
            return View();
        }
        public IActionResult Logo()
        {
            return View();
        }
        public IActionResult Index()
        {
       
            try
            {
                // Check if the table exists
                if (!_context.Database.CanConnect() || !_context.TrackRecords.Any())
                {
                    ViewBag.Message = "No records available.";
                    return View(new List<TrackRecords>());
                }

                var records = _context.TrackRecords.ToList();
                return View(records);
            }
            catch (Exception)
            {
                ViewBag.Message = "No records available.";
                return View(new List<TrackRecords>());
            }
        }

        // Get the Preview Records 
        public IActionResult PreviewRecords(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return BadRequest("Table name is missing.");
            var storedData = GetTableData(tableName);
            ViewBag.TableName = tableName;
            ViewBag.FunctionalRecords = FilterRecords(storedData, "Functional");
            ViewBag.DefectiveRecords = FilterRecords(storedData, "Defective");
            ViewBag.UnknownRecords = storedData?.Columns.Contains("STATUS") == true
                ? FilterRecords(storedData, "UNKNOWN", true)
                : storedData;
            return View();
        }

        [HttpPost]
        public IActionResult DeleteRecords(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return BadRequest("Invalid table name.");
            }

            try
            {
                using var conn = new SqlConnection(_configuration.GetConnectionString("DNTS_CLISContext"));
                conn.Open();

                // Drop the related tables
                string dropTableQuery = $@"
            IF OBJECT_ID('{tableName}', 'U') IS NOT NULL DROP TABLE [{tableName}];";

                using (var dropCmd = new SqlCommand(dropTableQuery, conn))
                {
                    dropCmd.ExecuteNonQuery();
                }

                // Delete the record from TrackRecords table
                var trackRecord = _context.TrackRecords.FirstOrDefault(t => t.TrackNo == tableName);
                if (trackRecord != null)
                {
                    _context.TrackRecords.Remove(trackRecord);
                    _context.SaveChanges();
                }

                TempData["Message"] = "Record deleted successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error deleting record: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        private DataTable? FilterRecords(DataTable? data, string status, bool includeEmpty = false)
        {
            return data?.Select($"STATUS = '{status}'" + (includeEmpty ? " OR STATUS IS NULL OR STATUS = ''" : "")).Any() == true
                ? data.Select($"STATUS = '{status}'" + (includeEmpty ? " OR STATUS IS NULL OR STATUS = ''" : "")).CopyToDataTable()
                : null;
        }

        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file, string receiverName, DateTime? createdDate)
        {
            if (file == null || file.Length == 0 || string.IsNullOrEmpty(receiverName))
                return BadRequest("Invalid input.");

            var dataTable = await ProcessExcel(file);
            if (dataTable == null || dataTable.Rows.Count == 0)
                return BadRequest("Invalid Excel file.");

            // Retrieve the first value from the 'Particular' column if available
            string particularValue = dataTable.Columns.Contains("Particular") && dataTable.Rows.Count > 0
                ? dataTable.Rows[0]["Particular"].ToString().Replace(" ", "_") // Replace spaces with underscores
                : "Unknown";

            string baseTrackNo = $"TrackNo_{DateTime.Now:ffff}_{particularValue}";
            string uploaderTable = baseTrackNo;
            string taTable = baseTrackNo + "_TA.xlsx";

            // Create both tables dynamically
            CreateDynamicTable(uploaderTable, dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList());
            CreateDynamicTable(taTable, dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList());

            foreach (DataRow row in dataTable.Rows)
            {
                var columns = dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
                var values = row.ItemArray.Select(v => v.ToString()).ToList();

                // Insert into both tables
                InsertData(uploaderTable, columns, values);
                InsertData(taTable, columns, values);
            }

            // Save to TrackRecords Table
            var trackRecord = new TrackRecords
            {
                TrackNo = uploaderTable,
                ReceiverName = receiverName,
                CreatedDate = createdDate ?? DateTime.Now
            };

            _context.TrackRecords.Add(trackRecord);
            await _context.SaveChangesAsync();

            List<TrackRecords> uploadedRecords = _context.TrackRecords.ToList();

            TempData["UploadedRecords"] = JsonConvert.SerializeObject(uploadedRecords);
            TempData["LatestTableName"] = uploaderTable;
            TempData.Keep("LatestTableName");

            return RedirectToAction("Index");
        }

        private async Task<DataTable?> ProcessExcel(IFormFile file)
        {
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets[0];
            if (worksheet == null) return null;

            var dataTable = new DataTable();
            List<int> validColumns = new List<int>();

            // Process headers (Row 4) and filter out empty ones
            for (int col = 1; col <= worksheet.Dimension?.Columns; col++)
            {
                string? headerName = worksheet.Cells[4, col]?.Value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(headerName))
                {
                    headerName = Regex.Replace(headerName, "[^\\w]", "").Replace(" ", "_");
                    dataTable.Columns.Add(headerName);
                    validColumns.Add(col); // Track only valid columns
                }
            }

            // Ensure "STATUS" column exists
            if (!dataTable.Columns.Contains("STATUS"))
                dataTable.Columns.Add("STATUS", typeof(string));

            // Process data rows (starting from row 5)
            for (int row = 5; row <= worksheet.Dimension?.Rows; row++)
            {
                var newRow = dataTable.NewRow();
                bool isEmptyRow = true;

                foreach (int col in validColumns) // Only process valid columns
                {
                    string cellValue = worksheet.Cells[row, col]?.Value?.ToString()?.Trim() ?? "";
                    newRow[col - 1] = cellValue;

                    if (!string.IsNullOrWhiteSpace(cellValue))
                        isEmptyRow = false;
                }

                if (!isEmptyRow)
                {
                    if (string.IsNullOrWhiteSpace(newRow["STATUS"].ToString()))
                        newRow["STATUS"] = "UNKNOWN";
                    dataTable.Rows.Add(newRow);
                }
            }

            return dataTable;
        }

        private void CreateDynamicTable(string tableName, List<string> columns)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DNTS_CLISContext"));
            conn.Open();

            // Remove unwanted columns (like Column9, empty, or invalid columns)
            columns = columns.Where(c => !string.IsNullOrEmpty(c) && !c.StartsWith("Column")).ToList();

            string createTableQuery = $"CREATE TABLE [{tableName}] (ID INT IDENTITY(1,1) PRIMARY KEY, {string.Join(", ", columns.Select(c => $"[{c}] NVARCHAR(MAX)"))})";

            using var cmd = new SqlCommand(createTableQuery, conn);
            cmd.ExecuteNonQuery();
        }


        private void InsertData(string tableName, List<string> columns, List<string> values)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DNTS_CLISContext"));
            conn.Open();
            string insertQuery = $"INSERT INTO [{tableName}] ({string.Join(", ", columns.Select(c => $"[{c}]"))}) VALUES ({string.Join(", ", columns.Select((c, i) => $"@val{i}"))})";
            using var cmd = new SqlCommand(insertQuery, conn);
            for (int i = 0; i < values.Count; i++)
                cmd.Parameters.AddWithValue($"@val{i}", values[i] ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private DataTable GetTableData(string tableName)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString("DNTS_CLISContext"));
            conn.Open();
            using var cmd = new SqlCommand($"SELECT * FROM [{tableName}]", conn);
            using var adapter = new SqlDataAdapter(cmd);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);
            return dataTable;
        }
    }
}
