using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using AssetItems.Models;
using AssetHistory.Models;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;


namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssetDisposalApiController : ControllerBase
    {
        
        private readonly string _connectionString = "Data Source=capstone.db";

     [HttpGet("GetAssetSummaries")]
public async Task<IActionResult> GetAssetSummariesAsync()
{
    const string query = @"
        SELECT 
            ai.AssetID AS AssetId,
            ai.AssetName,
            ai.AssetCode,
            ac.CategoryName AS AssetCategory,
            ai.CategoryID AS AssetCategoryId,
            ai.DatePurchased AS PurchaseDate,
            ai.WarrantyExpirationDate AS WarrantyExpiration,
            ai.AssetVendor AS Vendor,
            ai.AssetCost AS CurrentValue
        FROM asset_item_db ai
        LEFT JOIN asset_category_tb ac ON ai.CategoryID = ac.CategoryId
        ORDER BY ai.AssetName ASC;
    ";

    try
    {
        using (var connection = new SqliteConnection(_connectionString))
        {
            var results = await connection.QueryAsync(query);
            return Ok(results);
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


      [HttpPost("DisposeAsset")]
        public async Task<IActionResult> DisposeAssetAsync([FromBody] DisposedAsset request)
        {
            if (request == null || request.AssetID <= 0 || request.CategoryID <= 0)
                return BadRequest("Invalid asset disposal request.");

            const string insertDisposalQuery = @"
                INSERT INTO asset_disposed_tb 
                (AssetID, CategoryID, AssetName, AssetCode, DisposalDate, DisposalReason, OriginalValue, DisposedValue, LossValue)
                VALUES 
                (@AssetID, @CategoryID, @AssetName, @AssetCode, @DisposalDate, @DisposalReason, @OriginalValue, @DisposedValue, @LossValue);";

            const string updateAssetStatusQuery = @"
                UPDATE asset_item_db 
                SET AssetStatus = @DisposalReason 
                WHERE AssetID = @AssetID;";

            const string insertNotificationQuery = @"
                INSERT INTO AssetNotifications_tb 
                (Type, AssetId, AssetName, AssetCode, CategoryId, Message, Date, Priority, Read)
                VALUES 
                ('Disposal', @AssetID, @AssetName, @AssetCode, @CategoryID, @Message, CURRENT_TIMESTAMP, 'High', 0);";

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = await connection.BeginTransactionAsync();

                // Insert into disposal table
                await connection.ExecuteAsync(insertDisposalQuery, request, transaction);

                // Update asset status to match disposal reason
                await connection.ExecuteAsync(updateAssetStatusQuery, new
                {
                    request.DisposalReason,
                    request.AssetID
                }, transaction);

                // Insert disposal notification
                string message = $"Asset {request.AssetCode} was disposed due to: {request.DisposalReason}.";
                await connection.ExecuteAsync(insertNotificationQuery, new
                {
                    request.AssetID,
                    request.AssetName,
                    request.AssetCode,
                    request.CategoryID,
                    Message = message
                }, transaction);

                await transaction.CommitAsync();

                return Ok(new { Message = "Asset disposed, status updated, and notification sent." });
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


        [HttpGet("GetDisposedAssets")]
public async Task<IActionResult> GetDisposedAssetsAsync()
{
    const string query = @"
        SELECT 
            AssetID,
            CategoryID,
            AssetName,
            AssetCode,
            DisposalDate,
            DisposalReason,
            OriginalValue,
            DisposedValue,
            LossValue
        FROM asset_disposed_tb
        ORDER BY DisposalDate DESC;
    ";

    try
    {
        using (var connection = new SqliteConnection(_connectionString))
        {
            var disposedAssets = await connection.QueryAsync(query);
            return Ok(disposedAssets);
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
}