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
            const string query = "SELECT * FROM asset_item_tb WHERE CategoryID = @CategoryID";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var assets = await connection.QueryAsync<AssetItem>(query, new { CategoryID = categoryID });
                return Ok(assets); // Return filtered assets based on CategoryID
            }
        }

      
[HttpPost("InsertAsset")]
public async Task<IActionResult> InsertAssetAsync(AssetItem newAsset)
{
    const string insertQuery = @"
    INSERT INTO asset_item_db 
    (
        CategoryID, AssetQRCode, AssetName, AssetPicture, DatePurchased, DateIssued, IssuedTo, 
        AssetVendor, CheckedBy, AssetCost, AssetCode, Remarks, AssetLocation, 
        WarrantyStartDate, WarrantyExpirationDate, WarrantyVendor, WarrantyContact, 
        AssetStatus, AssetType, PreventiveMaintenanceSchedule, Notes, 
        OperationStartDate, OperationEndDate, DisposalDate, DisposalMethod, 
        TransferredTo, TransferDate
    ) 
    VALUES 
    (
        @CategoryID, @AssetQRCode, @AssetName, @AssetPicture, @DatePurchased, @DateIssued, @IssuedTo, 
        @AssetVendor, @CheckedBy, @AssetCost, @AssetCode, @Remarks, @AssetLocation, 
        @WarrantyStartDate, @WarrantyExpirationDate, @WarrantyVendor, @WarrantyContact, 
        @AssetStatus, @AssetType, @PreventiveMaintenanceSchedule, @Notes, 
        @OperationStartDate, @OperationEndDate, @DisposalDate, @DisposalMethod, 
        @TransferredTo, @TransferDate
    );
    SELECT * FROM asset_item_db ORDER BY AssetID DESC LIMIT 1;";

    using (var connection = new SqliteConnection(_connectionString))
    {
        connection.Open();

        // Insert asset and retrieve the newly created asset
        var result = await connection.QuerySingleOrDefaultAsync<AssetItem>(insertQuery, new
        {
            newAsset.CategoryID,
            newAsset.AssetQRCode,
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
            newAsset.AssetType,
            newAsset.PreventiveMaintenanceSchedule,
            newAsset.Notes,
            newAsset.OperationStartDate,
            newAsset.OperationEndDate,
            newAsset.DisposalDate,
            newAsset.DisposalMethod,
            newAsset.TransferredTo,
            newAsset.TransferDate
        });

        if (result == null)
        {
            return BadRequest("Failed to insert the asset.");
        }

        // Calculate depreciation schedule for the inserted asset
        await CalculateDepreciationScheduleAsync(result, connection);

        return Ok(result); // Return the newly inserted asset
    }
}

private async Task CalculateDepreciationScheduleAsync(AssetItem asset, SqliteConnection connection)
{
    decimal currentValue = asset.Cost;
    decimal depreciationAmountPerPeriod = (asset.DepreciationRate ?? 0) / 100 * asset.Cost;

    const string insertDepreciationQuery = @"
    INSERT INTO asset_depreciation_tb 
    (AssetID, DepreciationDate, DepreciationValue, RemainingValue) 
    VALUES 
    (@AssetID, @DepreciationDate, @DepreciationValue, @RemainingValue);";

    // Handle depreciation based on the period type (year or month)
    if (asset.DepreciationPeriodType == "year")
    {
        for (int i = 1; currentValue > 1; i += asset.DepreciationPeriodValue)
        {
            currentValue -= depreciationAmountPerPeriod;
            if (currentValue < 1) currentValue = 1; // Ensure value does not go below 1

            await connection.ExecuteAsync(insertDepreciationQuery, new
            {
                AssetID = asset.AssetId,
                DepreciationDate = asset.DatePurchased.AddYears(i),
                DepreciationValue = depreciationAmountPerPeriod,
                RemainingValue = currentValue
            });
        }
    }
    else if (asset.DepreciationPeriodType == "month")
    {
        for (int i = 1; currentValue > 1; i += asset.DepreciationPeriodValue)
        {
            currentValue -= depreciationAmountPerPeriod;
            if (currentValue < 1) currentValue = 1; // Ensure value does not go below 1

            await connection.ExecuteAsync(insertDepreciationQuery, new
            {
                AssetID = asset.AssetId,
                DepreciationDate = asset.DatePurchased.AddMonths(i),
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
