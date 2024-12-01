using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using Items.Models;
using System.Threading.Tasks;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserItemApiController : ControllerBase
    {
        private readonly string _connectionString = "Data Source=capstone.db";

        // GET: api/ItemApi/GetItemCountsByCategory

        [HttpGet("GetAllItems")]
        public async Task<IActionResult> GetItemsAsync()
        {
            const string query = "SELECT *FROM items_db";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var items = await connection.QueryAsync<Item>(query);
                return Ok(items);
            }
        }
        [HttpGet("GetItemCountsByCategory")]
        public async Task<IActionResult> GetItemCountsByCategoryAsync()
        {
            const string query = @"
                SELECT c.Id AS CategoryID, c.CategoryName, COUNT(i.ItemID) AS ItemCount 
                FROM category_tb c
                LEFT JOIN items_db i ON c.Id = i.CategoryID
                GROUP BY c.Id, c.CategoryName";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var counts = await connection.QueryAsync<CategoryItemCount>(query);
                return Ok(counts); // Return a list of categories with item counts
            }
        }

        // Class to hold the response data
        public class CategoryItemCount
        {
            public int CategoryID { get; set; }
            public string CategoryName { get; set; } // Added CategoryName property
            public int ItemCount { get; set; }
        }
    }
}
