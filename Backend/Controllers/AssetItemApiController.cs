using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using AssetItems.Models;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssetItemApiController : ControllerBase
    {

        private readonly string _connectionString = "Data Source=capstone.db";

        // GET: api/AssetApi/GetAssetsByCategory?categoryID=1
        [HttpGet("GetAssetsByCategory")]
        public async Task<IActionResult> GetAssetsByCategoryAsync(int categoryID)
        {
            const string query = "SELECT * FROM asset_item_db WHERE CategoryID = @CategoryID";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var assets = await connection.QueryAsync<AssetItem>(query, new { CategoryID = categoryID });

                // Convert byte[] to Base64 string for JSON response

                return Ok(assets);
            }
        }
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

    const string insertAssetHistoryQuery = @"
        INSERT INTO asset_history 
        (AssetID, ActionType, ActionDate, PerformedBy, Remarks) 
        VALUES 
        (@AssetID, @ActionType, CURRENT_TIMESTAMP, @PerformedBy, @Remarks);";

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
                // Update asset details
                await connection.ExecuteAsync(updateAssetQuery, new
                {
                    request.AssetID,
                    request.NewOwner,
                    request.NewLocation
                }, transaction);

                // Insert transfer history
                await connection.ExecuteAsync(insertTransferHistoryQuery, new
                {
                    request.AssetID,
                    PreviousOwner = asset.IssuedTo ?? "Unknown",
                    NewOwner = request.NewOwner,
                    PreviousLocation = asset.AssetLocation ?? "Unknown",
                    NewLocation = request.NewLocation,
                    request.Remarks
                }, transaction);

                // Log asset history
                await connection.ExecuteAsync(insertAssetHistoryQuery, new
                {
                    request.AssetID,
                    ActionType = "Transfer",
                    PerformedBy = request.NewOwner, // Assuming the new owner performs the transfer
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

    [HttpPut("UpdateAssetStatus/{assetId}")]
public async Task<IActionResult> UpdateAssetStatusAsync(int assetId, [FromBody] string newStatus)
{
    if (string.IsNullOrWhiteSpace(newStatus))
    {
        return BadRequest("Status cannot be empty.");
    }

    const string updateQuery = @"
    UPDATE asset_item_db 
    SET AssetStatus = @AssetStatus 
    WHERE AssetID = @AssetID";

    try
    {
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();

            int rowsAffected = await connection.ExecuteAsync(updateQuery, new { AssetStatus = newStatus, AssetID = assetId });

            if (rowsAffected > 0)
            {
                return Ok(new { message = "Asset status updated successfully.", assetId, newStatus });
            }
            else
            {
                return NotFound("Asset not found.");
            }
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

        [HttpPost("InsertAsset")]
        public async Task<IActionResult> InsertAssetAsync([FromBody] AssetItem newAsset)
        {
            if (newAsset == null)
            {
                Console.WriteLine("Received null newAsset.");
                return BadRequest("Payload is invalid or missing.");
            }

            const string insertQuery = @"
            INSERT INTO asset_item_db 
            (CategoryID ,  AssetName, AssetPicture, DatePurchased, 
            DateIssued, IssuedTo, AssetVendor, CheckedBy, AssetCost, AssetCode, Remarks, 
            AssetLocation, WarrantyStartDate, WarrantyExpirationDate, WarrantyVendor, WarrantyContact, 
            AssetStatus, AssetStype, AssetPreventiveMaintenace, Notes, OperationStartDate, 
            OperationEndDate, DisposalDate) 
            VALUES 
            (@CategoryID,  @AssetName, @AssetPicture, @DatePurchased, 
            @DateIssued, @IssuedTo, @AssetVendor, @CheckedBy, @AssetCost, @AssetCode, @Remarks, 
            @AssetLocation, @WarrantyStartDate, @WarrantyExpirationDate, @WarrantyVendor, @WarrantyContact, 
            @AssetStatus, @AssetStype, @AssetPreventiveMaintenace, @Notes, @OperationStartDate, 
            @OperationEndDate, @DisposalDate);
            SELECT last_insert_rowid() AS AssetID;";

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    var assetId = await connection.ExecuteScalarAsync<int>(insertQuery, newAsset);

                    if (assetId <= 0)
                    {
                        return BadRequest("Failed to insert the asset.");
                    }

                    newAsset.AssetId = assetId;

                    if (newAsset.DepreciationRate.HasValue && newAsset.DepreciationPeriodValue > 0)
                    {
                        await CalculateDepreciationScheduleAsync(newAsset, connection);
                    }

                    return Ok(newAsset);
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

        private async Task CalculateDepreciationScheduleAsync(AssetItem asset, SqliteConnection connection)
        {
            if (!asset.DatePurchased.HasValue || asset.DepreciationRate == null || asset.DepreciationPeriodValue <= 0)
            {
                Console.WriteLine("Depreciation cannot be calculated due to missing values.");
                return;
            }

            decimal currentValue = asset.AssetCost;
            decimal depreciationAmountPerPeriod = (asset.DepreciationRate.Value / 100) * asset.AssetCost;

            const string insertDepreciationQuery = @"
    INSERT INTO asset_depreciation_tb 
    (AssetID, DepreciationDate, DepreciationValue, RemainingValue) 
    VALUES 
    (@AssetID, @DepreciationDate, @DepreciationValue, @RemainingValue);";

            if (asset.DepreciationPeriodType == "year")
            {
                for (int year = asset.DepreciationPeriodValue; currentValue > 1; year += asset.DepreciationPeriodValue)
                {
                    currentValue = Math.Max(1, currentValue - depreciationAmountPerPeriod);

                    await connection.ExecuteAsync(insertDepreciationQuery, new
                    {
                        AssetID = asset.AssetId,
                        DepreciationDate = asset.DatePurchased.Value.AddYears(year),
                        DepreciationValue = depreciationAmountPerPeriod,
                        RemainingValue = currentValue
                    });
                }
            }
            else if (asset.DepreciationPeriodType == "month")
            {
                for (int month = asset.DepreciationPeriodValue; currentValue > 1; month += asset.DepreciationPeriodValue)
                {
                    currentValue = Math.Max(1, currentValue - depreciationAmountPerPeriod);

                    await connection.ExecuteAsync(insertDepreciationQuery, new
                    {
                        AssetID = asset.AssetId,
                        DepreciationDate = asset.DatePurchased.Value.AddMonths(month),
                        DepreciationValue = depreciationAmountPerPeriod,
                        RemainingValue = currentValue
                    });
                }
            }
        }

        [HttpGet("ViewDepreciationSchedule")]
        public async Task<IActionResult> ViewDepreciationScheduleAsync(int assetId)
        {
            const string query = @"
    SELECT DepreciationDate, DepreciationValue, RemainingValue 
    FROM asset_depreciation_tb 
    WHERE AssetID = @AssetID
    ORDER BY DepreciationDate ASC";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var depreciationSchedule = await connection.QueryAsync(query, new { AssetID = assetId });
                return Ok(depreciationSchedule); // Return all depreciation records for this asset
            }
        }
    }
}
