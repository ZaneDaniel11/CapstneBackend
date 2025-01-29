using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using AssetItems.Models;
using System.Threading.Tasks;
using System;

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
            const string query = "SELECT * FROM  asset_item_db WHERE CategoryID = @CategoryID";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var assets = await connection.QueryAsync<AssetItem>(query, new { CategoryID = categoryID });
                return Ok(assets); // Return filtered assets based on CategoryID
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
      Console.WriteLine($"Received Asset: {System.Text.Json.JsonSerializer.Serialize(newAsset)}");
    const string insertQuery = @"
    INSERT INTO asset_item_db 
    (
        CategoryID, AssetQRCodePath, AssetQRCodeBlob, AssetName, AssetPicture, DatePurchased, 
        DateIssued, IssuedTo, AssetVendor, CheckedBy, AssetCost, AssetCode, Remarks, 
        AssetLocation, WarrantyStartDate, WarrantyExpirationDate, WarrantyVendor, WarrantyContact, 
        AssetStatus, AssetStype, AssetPreventiveMaintenace, Notes, OperationStartDate, 
        OperationEndDate, DisposalDate
    ) 
    VALUES 
    (
        @CategoryID, @AssetQRCodePath, @AssetQRCodeBlob, @AssetName, @AssetPicture, @DatePurchased, 
        @DateIssued, @IssuedTo, @AssetVendor, @CheckedBy, @AssetCost, @AssetCode, @Remarks, 
        @AssetLocation, @WarrantyStartDate, @WarrantyExpirationDate, @WarrantyVendor, @WarrantyContact, 
        @AssetStatus, @AssetStype, @AssetPreventiveMaintenace, @Notes, @OperationStartDate, 
        @OperationEndDate, @DisposalDate
    );
    SELECT last_insert_rowid() AS AssetID;";

    try
    {
        using (var connection = new SqliteConnection(_connectionString))
        {
            connection.Open();

            // Insert asset and get the generated AssetID
            var assetId = await connection.ExecuteScalarAsync<int>(insertQuery, new
            {
                newAsset.CategoryID,
                newAsset.AssetQRCodePath,
                newAsset.AssetQRCodeBlob, // Base64-encoded string converted to byte[] by the framework
                newAsset.AssetName,
                newAsset.AssetPicture,
                newAsset.DatePurchased,
                newAsset.DateIssued,
                newAsset.IssuedTo,
                newAsset.AssetVendor,
                newAsset.CheckedBy,
                newAsset.AssetCost,
                newAsset.AssetCode,
                newAsset.Remarks,
                newAsset.AssetLocation,
                newAsset.WarrantyStartDate,
                newAsset.WarrantyExpirationDate,
                newAsset.WarrantyVendor,
                newAsset.WarrantyContact,
                newAsset.AssetStatus,
                newAsset.AssetStype,
                newAsset.AssetPreventiveMaintenace,
                newAsset.Notes,
                newAsset.OperationStartDate,
                newAsset.OperationEndDate,
                newAsset.DisposalDate
            });

            if (assetId <= 0)
            {
                return BadRequest("Failed to insert the asset.");
            }

            // Set the AssetID for further processing
            newAsset.AssetId = assetId;

            // Calculate depreciation schedule for the inserted asset
            await CalculateDepreciationScheduleAsync(newAsset, connection);

            return Ok(newAsset); // Return the newly inserted asset with depreciation details
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
    decimal currentValue = asset.AssetCost; // Asset cost at the time of purchase
    decimal depreciationAmountPerPeriod = (asset.DepreciationRate ?? 0) / 100 * asset.AssetCost;

    const string insertDepreciationQuery = @"
    INSERT INTO asset_depreciation_tb 
    (AssetID, DepreciationDate, DepreciationValue, RemainingValue) 
    VALUES 
    (@AssetID, @DepreciationDate, @DepreciationValue, @RemainingValue);";

    // Calculate depreciation based on the specified period type (yearly or monthly)
    if (asset.DepreciationPeriodType == "year")
    {
        for (int year = 1; currentValue > 1; year += asset.DepreciationPeriodValue)
        {
            currentValue -= depreciationAmountPerPeriod;
            if (currentValue < 1) currentValue = 1; // Ensure value does not drop below 1

            await connection.ExecuteAsync(insertDepreciationQuery, new
            {
                AssetID = asset.AssetId,
                DepreciationDate = asset.DatePurchased.AddYears(year),
                DepreciationValue = depreciationAmountPerPeriod,
                RemainingValue = currentValue
            });
        }
    }
    else if (asset.DepreciationPeriodType == "month")
    {
        for (int month = 1; currentValue > 1; month += asset.DepreciationPeriodValue)
        {
            currentValue -= depreciationAmountPerPeriod;
            if (currentValue < 1) currentValue = 1; // Ensure value does not drop below 1

            await connection.ExecuteAsync(insertDepreciationQuery, new
            {
                AssetID = asset.AssetId,
                DepreciationDate = asset.DatePurchased.AddMonths(month),
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
