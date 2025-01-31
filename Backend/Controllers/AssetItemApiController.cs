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
                foreach (var asset in assets)
                {
                    if (asset.AssetQRCodeBlob != null)
                    {
                        asset.AssetQRCodeBase64 = $"data:image/png;base64,{Convert.ToBase64String(asset.AssetQRCodeBlob)}";
                    }
                }

                return Ok(assets);
            }
        }
        private static byte[] ConvertBase64ToBytes(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
            {
                return null;
            }


            string base64Data = Regex.Replace(base64String, @"^data:image\/[a-zA-Z]+;base64,", "");
            Console.WriteLine($"Base64 Data After Stripping: {base64Data.Substring(0, 50)}..."); // Print first 50 chars for debugging

            try
            {
                return Convert.FromBase64String(base64Data);
            }
            catch (FormatException ex)
            {
                Console.WriteLine("Base64 conversion failed: " + ex.Message);
                return null;
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
            if (!string.IsNullOrEmpty(newAsset.AssetQRCodeBase64))
            {
                newAsset.AssetQRCodeBlob = ConvertBase64ToBytes(newAsset.AssetQRCodeBase64);
            }

            Console.WriteLine($"Received Asset: {System.Text.Json.JsonSerializer.Serialize(newAsset)}");

            const string insertQuery = @"
            INSERT INTO asset_item_db 
            (CategoryID, AssetQRCodePath, AssetQRCodeBlob, AssetName, AssetPicture, DatePurchased, 
            DateIssued, IssuedTo, AssetVendor, CheckedBy, AssetCost, AssetCode, Remarks, 
            AssetLocation, WarrantyStartDate, WarrantyExpirationDate, WarrantyVendor, WarrantyContact, 
            AssetStatus, AssetStype, AssetPreventiveMaintenace, Notes, OperationStartDate, 
            OperationEndDate, DisposalDate) 
            VALUES 
            (@CategoryID, @AssetQRCodePath, @AssetQRCodeBlob, @AssetName, @AssetPicture, @DatePurchased, 
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
