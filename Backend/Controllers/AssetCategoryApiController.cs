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

 [HttpPost("TransferAsset")]
        public async Task<IActionResult> TransferAssetAsync([FromBody] AssetTransferRequest request)
        {
            if (request == null || request.AssetID <= 0 || string.IsNullOrWhiteSpace(request.NewOwner) || string.IsNullOrWhiteSpace(request.NewLocation))
            {
                return BadRequest("Invalid asset transfer request.");
            }

            const string getAssetQuery = "SELECT AssetID, IssuedTo, AssetLocation FROM asset_item_db WHERE AssetID = @AssetID";

            const string updateAssetQuery = @"
                UPDATE asset_item_db 
                SET IssuedTo = @NewOwner, AssetLocation = @NewLocation 
                WHERE AssetID = @AssetID";

            const string insertTransferHistoryQuery = @"
                INSERT INTO asset_transfer_history_tb 
                (AssetID, PreviousOwner, NewOwner, PreviousLocation, NewLocation, TransferDate, Remarks) 
                VALUES 
                (@AssetID, @PreviousOwner, @NewOwner, @PreviousLocation, @NewLocation, CURRENT_TIMESTAMP, @Remarks);";

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var asset = await connection.QueryFirstOrDefaultAsync<AssetItem>(getAssetQuery, new { request.AssetID });

                    if (asset == null)
                    {
                        return NotFound("Asset not found.");
                    }

                    if (asset.IssuedTo == request.NewOwner && asset.AssetLocation == request.NewLocation)
                    {
                        return BadRequest("Asset is already assigned to this owner and location.");
                    }

                    using (var transaction = connection.BeginTransaction())
                    {
                        await connection.ExecuteAsync(updateAssetQuery, new
                        {
                            request.AssetID,
                            request.NewOwner,
                            request.NewLocation
                        }, transaction);

                        await connection.ExecuteAsync(insertTransferHistoryQuery, new
                        {
                            request.AssetID,
                            PreviousOwner = asset.IssuedTo ?? "Unknown",
                            NewOwner = request.NewOwner,
                            PreviousLocation = asset.AssetLocation ?? "Unknown",
                            NewLocation = request.NewLocation,
                            request.Remarks
                        }, transaction);

                        transaction.Commit();
                    }

                    return Ok(new { Message = "Asset transferred successfully.", AssetID = request.AssetID });
                }
            }
            catch (SqliteException ex)
            {
                return StatusCode(500, $"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }


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
