using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using AssetHistory.Models;
using System.Threading.Tasks;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssetHistoryApiController : ControllerBase
    {
        private readonly string _connectionString = "Data Source=capstone.db";
        [HttpGet("viewHistorical")]
        public async Task<IActionResult> ViewHistorical(int assetID)
        {
            const string query = @"
        SELECT HistoryID, AssetID, ActionType, ActionDate, PerformedBy, Remarks
        FROM asset_history
        WHERE AssetID = @AssetID
        ORDER BY ActionDate DESC"; // Sorting by newest first

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var history = await connection.QueryAsync<AssetHistory.Models.AssetHistory>(query, new { AssetID = assetID });

                return Ok(history);
            }

        }
    }
}