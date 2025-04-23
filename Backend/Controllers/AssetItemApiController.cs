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
    public class AssetItemApiController : ControllerBase
    {

        private readonly string _connectionString = "Data Source=capstone.db";


        [HttpGet("asset-category-summary")]
        public async Task<IActionResult> GetAssetCategorySummary(DateTime? startDate = null, DateTime? endDate = null)
        {
            endDate ??= DateTime.UtcNow; // Default to today's date if not provided

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
            WITH LatestDepreciation AS (
                SELECT d.AssetID, d.RemainingValue
                FROM asset_depreciation_tb d
                INNER JOIN (
                    SELECT AssetID, MAX(DepreciationDate) AS MaxDate
                    FROM asset_depreciation_tb
                    WHERE DepreciationDate <= @EndDate
                    GROUP BY AssetID
                ) latest ON d.AssetID = latest.AssetID AND d.DepreciationDate = latest.MaxDate
            )
            SELECT 
                ac.CategoryName, 
                COUNT(a.AssetID) AS AssetCount,
                SUM(COALESCE(CAST(ld.RemainingValue AS REAL), a.AssetCost * 1.0)) AS CurrentTotalValue,
                (SELECT COUNT(*) FROM asset_item_db) AS TotalAssetCount,
                (SELECT SUM(COALESCE(CAST(ld.RemainingValue AS REAL), a.AssetCost * 1.0)) FROM asset_item_db a LEFT JOIN LatestDepreciation ld ON a.AssetID = ld.AssetID) AS TotalAssetValue,
                (SELECT AVG(COALESCE(CAST(ld.RemainingValue AS REAL), a.AssetCost * 1.0)) FROM asset_item_db a LEFT JOIN LatestDepreciation ld ON a.AssetID = ld.AssetID) AS AverageAssetValue
            FROM asset_category_tb ac
            LEFT JOIN asset_item_db a ON ac.CategoryId = a.CategoryID
            LEFT JOIN LatestDepreciation ld ON a.AssetID = ld.AssetID
            GROUP BY ac.CategoryName;";

                var result = await connection.QueryAsync(query, new { EndDate = endDate });

                return Ok(result);
            }
        }


        [HttpGet("asset-category-summary-detailed")]
        public async Task<IActionResult> GetAssetCategorySummaryDetailed(DateTime? startDate = null, DateTime? endDate = null)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query;

                if (startDate.HasValue && endDate.HasValue)
                {
                    query = @"
                WITH RelevantDepreciation AS (
                    SELECT d.AssetID, d.RemainingValue, d.DepreciationDate
                    FROM asset_depreciation_tb d
                    INNER JOIN (
                        SELECT AssetID, MAX(DepreciationDate) AS MaxDate
                        FROM asset_depreciation_tb
                        WHERE DepreciationDate <= @EndDate
                        GROUP BY AssetID
                    ) latest ON d.AssetID = latest.AssetID AND d.DepreciationDate = latest.MaxDate
                    WHERE d.DepreciationDate >= @StartDate
                )
                SELECT 
                    ac.CategoryId,
                    ac.CategoryName, 
                    a.AssetID,
                    a.AssetName,
                    a.AssetCode,
                    a.DatePurchased,
                    a.AssetCost,
                    rd.DepreciationDate,
                    COALESCE(CAST(rd.RemainingValue AS REAL), a.AssetCost * 1.0) AS RemainingValue
                FROM asset_category_tb ac
                LEFT JOIN asset_item_db a ON ac.CategoryId = a.CategoryID
                LEFT JOIN RelevantDepreciation rd ON a.AssetID = rd.AssetID
                ORDER BY ac.CategoryName, a.AssetName;";

                    var result = await connection.QueryAsync(query, new { StartDate = startDate, EndDate = endDate });
                    return Ok(result);
                }
                else
                {
                    query = @"
                WITH LatestDepreciation AS (
                    SELECT d.AssetID, d.RemainingValue, d.DepreciationDate
                    FROM asset_depreciation_tb d
                    INNER JOIN (
                        SELECT AssetID, MAX(DepreciationDate) AS MaxDate
                        FROM asset_depreciation_tb
                        GROUP BY AssetID
                    ) latest ON d.AssetID = latest.AssetID AND d.DepreciationDate = latest.MaxDate
                )
                SELECT 
                    ac.CategoryId,
                    ac.CategoryName, 
                    a.AssetID,
                    a.AssetName,
                    a.AssetCode,
                    a.DatePurchased,
                    a.AssetCost,
                    ld.DepreciationDate,
                    COALESCE(CAST(ld.RemainingValue AS REAL), a.AssetCost * 1.0) AS RemainingValue
                FROM asset_category_tb ac
                LEFT JOIN asset_item_db a ON ac.CategoryId = a.CategoryID
                LEFT JOIN LatestDepreciation ld ON a.AssetID = ld.AssetID
                ORDER BY ac.CategoryName, a.AssetName;";

                    var result = await connection.QueryAsync(query);
                    return Ok(result);
                }
            }
        }

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
    if (request == null || request.AssetID <= 0 || string.IsNullOrWhiteSpace(request.NewOwner) || 
        string.IsNullOrWhiteSpace(request.NewLocation) || string.IsNullOrWhiteSpace(request.PerformedBy))
    {
        return BadRequest("Invalid asset transfer request.");
    }

    const string getAssetQuery = @"
        SELECT AssetID, AssetName, AssetCode, CategoryID, IssuedTo, AssetLocation 
        FROM asset_item_db 
        WHERE AssetID = @AssetID";

    const string updateAssetQuery = @"
        UPDATE asset_item_db 
        SET IssuedTo = @NewOwner, AssetLocation = @NewLocation 
        WHERE AssetID = @AssetID";

    const string insertTransferHistoryQuery = @"
        INSERT INTO asset_transfer_history_tb 
        (AssetID, AssetCode, CategoryId, PreviousOwner, NewOwner, PreviousLocation, NewLocation, TransferDate, Remarks) 
        VALUES 
        (@AssetID, @AssetCode, @CategoryId, @PreviousOwner, @NewOwner, @PreviousLocation, @NewLocation, CURRENT_TIMESTAMP, @Remarks);";

    const string insertAssetHistoryQuery = @"
        INSERT INTO asset_history 
        (AssetID, AssetCode, CategoryId, ActionType, ActionDate, PerformedBy, Remarks) 
        VALUES 
        (@AssetID, @AssetCode, @CategoryId, 'Transfer', CURRENT_TIMESTAMP, @PerformedBy, @Remarks);";

    const string insertNotificationQuery = @"
        INSERT INTO AssetNotifications_tb 
        (Type, AssetId, AssetName, AssetCode, CategoryId, Message, Date, Priority, Read) 
        VALUES 
        ('Transfer', @AssetID, @AssetName, @AssetCode, @CategoryId, @Message, CURRENT_TIMESTAMP, 'Medium', 0);";

    try
    {
        using (var connection = new SqliteConnection(_connectionString))
        {
            await connection.OpenAsync();
            var asset = await connection.QueryFirstOrDefaultAsync<AssetItem>(getAssetQuery, new { request.AssetID });

            if (asset == null)
            {
                return NotFound("Asset not found.");
            }

            if (asset.IssuedTo == request.NewOwner && asset.AssetLocation == request.NewLocation)
            {
                return BadRequest("Asset is already assigned to this owner and location.");
            }

            using (var transaction = await connection.BeginTransactionAsync())
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
                    asset.AssetCode,
                    CategoryId = asset.CategoryID,  // ✅ Corrected Parameter Name
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
                    asset.AssetCode,
                    CategoryId = asset.CategoryID,  // ✅ Corrected Parameter Name
                    request.PerformedBy,
                    request.Remarks
                }, transaction);

                // Insert notification
                string notificationMessage = $"Asset {asset.AssetCode} transferred to {request.NewOwner} at {request.NewLocation} by {request.PerformedBy}.";
                await connection.ExecuteAsync(insertNotificationQuery, new
                {
                    request.AssetID,
                    asset.AssetName,
                    asset.AssetCode,
                    CategoryId = asset.CategoryID,  // ✅ Corrected Parameter Name
                    Message = notificationMessage
                }, transaction);

                await transaction.CommitAsync();
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
    (CategoryID, AssetName, AssetPicture, DatePurchased, DateIssued, IssuedTo, 
     AssetVendor, CheckedBy, AssetCost, AssetCode, Remarks, AssetLocation, 
     WarrantyStartDate, WarrantyExpirationDate, WarrantyVendor, WarrantyContact, 
     AssetStatus, AssetStype, AssetPreventiveMaintenace, Notes, OperationStartDate, 
     OperationEndDate, DisposalDate) 
    VALUES 
    (@CategoryID, @AssetName, @AssetPicture, @DatePurchased, @DateIssued, @IssuedTo, 
     @AssetVendor, @CheckedBy, @AssetCost, @AssetCode, @Remarks, @AssetLocation, 
     @WarrantyStartDate, @WarrantyExpirationDate, @WarrantyVendor, @WarrantyContact, 
     @AssetStatus, @AssetStype, @AssetPreventiveMaintenace, @Notes, @OperationStartDate, 
     @OperationEndDate, @DisposalDate);
    SELECT last_insert_rowid() AS AssetID;";

            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    var assetId = await connection.ExecuteScalarAsync<int>(insertQuery, newAsset);

                    if (assetId <= 0)
                    {
                        return BadRequest("Failed to insert the asset.");
                    }

                    newAsset.AssetId = assetId;

                    // Calculate and insert depreciation if applicable
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
    (AssetID, DepreciationDate, DepreciationValue, RemainingValue, DepreciationRate, DepreciationPeriodType, DepreciationPeriodValue) 
    VALUES 
    (@AssetID, @DepreciationDate, @DepreciationValue, @RemainingValue, @DepreciationRate, @DepreciationPeriodType, @DepreciationPeriodValue);";

            DateTime depreciationDate = asset.DatePurchased.Value;

            while (currentValue > 1)
            {
                currentValue = Math.Max(1, currentValue - depreciationAmountPerPeriod);

                Console.WriteLine($"Inserting Depreciation Record: AssetID={asset.AssetId}, Date={depreciationDate}, Value={depreciationAmountPerPeriod}, Remaining={currentValue}, Rate={asset.DepreciationRate}, PeriodType={asset.DepreciationPeriodType}, PeriodValue={asset.DepreciationPeriodValue}");

                await connection.ExecuteAsync(insertDepreciationQuery, new
                {
                    AssetID = asset.AssetId,
                    DepreciationDate = depreciationDate,
                    DepreciationValue = depreciationAmountPerPeriod,
                    RemainingValue = currentValue,
                    DepreciationRate = asset.DepreciationRate.Value,
                    DepreciationPeriodType = asset.DepreciationPeriodType ?? "year", // Default to "year"
                    DepreciationPeriodValue = asset.DepreciationPeriodValue > 0 ? asset.DepreciationPeriodValue : 1 // Default to 1 if missing
                });

                // Adjust Depreciation Date
                if (asset.DepreciationPeriodType == "year")
                {
                    depreciationDate = depreciationDate.AddYears(asset.DepreciationPeriodValue);
                }
                else if (asset.DepreciationPeriodType == "month")
                {
                    depreciationDate = depreciationDate.AddMonths(asset.DepreciationPeriodValue);
                }
            }
        }


        [HttpGet("ViewDepreciationSchedule")]
        public async Task<IActionResult> ViewDepreciationScheduleAsync(int assetId)
        {
            const string query = @"
    SELECT DepreciationDate, DepreciationValue, RemainingValue, 
           DepreciationRate, DepreciationPeriodType, DepreciationPeriodValue
    FROM asset_depreciation_tb 
    WHERE AssetID = @AssetID
    ORDER BY DepreciationDate ASC";

            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync();
                var depreciationSchedule = await connection.QueryAsync(query, new { AssetID = assetId });

                if (!depreciationSchedule.Any())
                {
                    return NotFound($"No depreciation records found for AssetID {assetId}.");
                }

                return Ok(depreciationSchedule);
            }
        }

    }
}
