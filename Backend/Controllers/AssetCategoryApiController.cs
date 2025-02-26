using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using AssetCategorys.Models;
using System.Threading.Tasks;

namespace Backend.Controllers
{
    [ApiController] // Ensure this is only declared once
    [Route("api/[controller]")]
    public class CategoryAssetApiController : ControllerBase
    {
        private readonly string _connectionString = "Data Source=capstone.db";

 

        [HttpGet("GetAssetCategory")]
        public async Task<IActionResult> GetAsssetCategoryAsync()
        {
            const string query = "SELECT * FROM asset_category_tb";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var category = await connection.QueryAsync<AssetCategory>(query);
                return Ok(category);
            }
        }

      [HttpPost("InsertAssetCategory")]
    public async Task<IActionResult> InsertAsssetCategoryAsync(AssetCategory cat)
    {
        const string query = @"
            INSERT INTO asset_category_tb (CategoryName) 
            VALUES (@CategoryName);
            SELECT last_insert_rowid() AS CategoryId;";  // Fetch the last inserted ID

        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();
            var newCategoryId = await connection.ExecuteScalarAsync<int>(query, new { CategoryName = cat.CategoryName });
            cat.CategoryId = newCategoryId;  // Assign the new CategoryId back to the category object
            return Ok(cat);  // Return the full category object, including its new CategoryId
        }
    }


        [HttpDelete("DeleteAssetCategory")]
        public async Task<IActionResult> DeleteAsssetCategoryAsync(int CategoryId)
        {
            const string query = "DELETE FROM asset_category_tb WHERE CategoryId = @CategoryId";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var result = await connection.ExecuteAsync(query, new { CategoryId });
                return Ok(new { success = true });
            }
        }

        [HttpPut("UpdateAssetCategory")]
        public async Task<IActionResult> UpdateAsssetCategoryAsync(int CategoryId, AssetCategory cat)
        {
            const string query = @"
                UPDATE asset_category_tb
                SET CategoryName = @CategoryName 
                WHERE CategoryId = @CategoryId; 
                SELECT * FROM asset_category_tb WHERE CategoryId = @CategoryId LIMIT 1;";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var result = await connection.QuerySingleOrDefaultAsync<AssetCategory>(query, new { CategoryId, CategoryName = cat.CategoryName });
                return Ok(result);
            }
        }
    }
}
