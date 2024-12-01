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

        [HttpGet("GetAllAssetCodes")]
        public async Task<IActionResult> GetAllAssetCodesAsync()
        {
            const string query = "SELECT AssetCode FROM asset_item_tb";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                // Query for AssetCode column only
                var assetCodes = await connection.QueryAsync<string>(query);
                return Ok(assetCodes); // Return the list of AssetCode values
            }
        }

        // POST: api/AssetApi/InsertAsset
        [HttpPost("InsertAsset")]
        public async Task<IActionResult> InsertAssetAsync(AssetItem newAsset)
        {
            const string insertQuery = @"
        INSERT INTO asset_item_tb 
        (CategoryID, AssetName, DatePurchased, DateIssued, IssuedTo, CheckedBy, Cost, Location, AssetCode, Remarks, DepreciationRate, DepreciationValue, DepreciationPeriodType, DepreciationPeriodValue) 
        VALUES 
        (@CategoryID, @AssetName, @DatePurchased, @DateIssued, @IssuedTo, @CheckedBy, @Cost, @Location, @AssetCode, @Remarks, @DepreciationRate, @DepreciationValue, @DepreciationPeriodType, @DepreciationPeriodValue);
        SELECT * FROM asset_item_tb ORDER BY AssetId DESC LIMIT 1;";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                // Insert asset and retrieve the newly created asset
                var result = await connection.QuerySingleOrDefaultAsync<AssetItem>(insertQuery, new
                {
                    newAsset.CategoryID,
                    newAsset.AssetName,
                    newAsset.DatePurchased,
                    newAsset.DateIssued,
                    newAsset.IssuedTo,
                    newAsset.CheckedBy,
                    newAsset.Cost,
                    newAsset.Location,
                    newAsset.AssetCode,
                    newAsset.Remarks,
                    newAsset.DepreciationRate,
                    newAsset.DepreciationValue,
                    newAsset.DepreciationPeriodType, // "month" or "year"
                    newAsset.DepreciationPeriodValue  // Interval in months or years
                });

                // Calculate depreciation schedule for the inserted asset
                await CalculateDepreciationScheduleAsync(result, connection);

                return Ok(result); // Return the newly inserted asset
            }
        }

        // Method to calculate and store depreciation schedule
        private async Task CalculateDepreciationScheduleAsync(AssetItem asset, SqliteConnection connection)
        {
            decimal currentValue = asset.Cost;
            decimal depreciationAmountPerPeriod = (asset.DepreciationRate ?? 0) / 100 * asset.Cost;

            // Determine the number of periods based on DepreciationPeriodType (year or month)
            if (asset.DepreciationPeriodType == "year")
            {
                // Depreciate every N years
                for (int i = 1; currentValue > 1; i += asset.DepreciationPeriodValue)
                {
                    currentValue -= depreciationAmountPerPeriod;

                    // Ensure the asset value does not drop below 1
                    if (currentValue < 1)
                    {
                        currentValue = 1;
                    }

                    // Insert depreciation record for this period
                    const string insertDepreciationQuery = @"
                INSERT INTO depreciation_tb (AssetId, DepreciationDate, DepreciationValue) 
                VALUES (@AssetId, @DepreciationDate, @DepreciationValue);";

                    await connection.ExecuteAsync(insertDepreciationQuery, new
                    {
                        AssetId = asset.AssetId,
                        DepreciationDate = asset.DatePurchased.AddYears(i), // Every N years
                        DepreciationValue = currentValue
                    });
                }
            }
            else if (asset.DepreciationPeriodType == "month")
            {
                // Depreciate every N months
                for (int i = 1; currentValue > 1; i += asset.DepreciationPeriodValue)
                {
                    currentValue -= depreciationAmountPerPeriod;

                    // Ensure the asset value does not drop below 1
                    if (currentValue < 1)
                    {
                        currentValue = 1;
                    }

                    // Insert depreciation record for this period
                    const string insertDepreciationQuery = @"
                INSERT INTO depreciation_tb (AssetId, DepreciationDate, DepreciationValue) 
                VALUES (@AssetId, @DepreciationDate, @DepreciationValue);";

                    await connection.ExecuteAsync(insertDepreciationQuery, new
                    {
                        AssetId = asset.AssetId,
                        DepreciationDate = asset.DatePurchased.AddMonths(i), // Every N months
                        DepreciationValue = currentValue
                    });
                }
            }
        }

        [HttpGet("ViewDepreciationSchedule")]
        public async Task<IActionResult> ViewDepreciationScheduleAsync(int assetId)
        {
            const string query = @"
        SELECT DepreciationDate, DepreciationValue 
        FROM depreciation_tb 
        WHERE AssetId = @AssetId
        ORDER BY DepreciationDate ASC";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var depreciationSchedule = await connection.QueryAsync(query, new { AssetId = assetId });
                return Ok(depreciationSchedule); // Return all depreciation records for this asset
            }
        }

        // DELETE: api/AssetApi/DeleteAsset?AssetID=1
        [HttpDelete("DeleteAsset")]
        public async Task<IActionResult> DeleteAssetAsync(int CategoryID, string AssetCode)
        {
            const string query = "DELETE FROM asset_item_tb WHERE CategoryID = @CategoryID AND AssetCode = @AssetCode";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var result = await connection.ExecuteAsync(query, new { CategoryID, AssetCode });
                return Ok(new { success = result > 0 });
            }
        }

        // PUT: api/AssetApi/UpdateAsset
        [HttpPut("UpdateAsset")]
        public async Task<IActionResult> UpdateAssetAsync(int CategoryID, string AssetCode, AssetItem updatedAsset)
        {
            const string query = @"
                UPDATE asset_item_tb 
                SET 
                    AssetName = @AssetName, 
                    DatePurchased = @DatePurchased,
                    DateIssued = @DateIssued,
                    IssuedTo = @IssuedTo,
                    CheckedBy = @CheckedBy,
                    Cost = @Cost,
                    Location = @Location,
                    Remarks = @Remarks,
                    DepreciationRate = @DepreciationRate,
                    DepreciationValue = @DepreciationValue
                WHERE CategoryID = @CategoryID AND AssetCode = @AssetCode;
                SELECT * FROM asset_item_tb WHERE CategoryID = @CategoryID AND AssetCode = @AssetCode LIMIT 1;";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var result = await connection.QuerySingleOrDefaultAsync<AssetItem>(query, new
                {
                    updatedAsset.AssetName,
                    updatedAsset.DatePurchased,
                    updatedAsset.DateIssued,
                    updatedAsset.IssuedTo,
                    updatedAsset.CheckedBy,
                    updatedAsset.Cost,
                    updatedAsset.Location,
                    updatedAsset.Remarks,
                    updatedAsset.DepreciationRate,
                    updatedAsset.DepreciationValue,
                    CategoryID,
                    AssetCode
                });

                return Ok(result);
            }
        }

        // Scheduled task to calculate depreciation


        // PUT: api/AssetApi/UpdateDepreciationValues
        [HttpPut("UpdateDepreciationValues")]
        public async Task<IActionResult> UpdateDepreciationValuesAsync()
        {
            const string query = @"
        SELECT d.AssetId, d.DepreciationDate, d.DepreciationValue
        FROM depreciation_tb d
        JOIN asset_item_tb a ON d.AssetId = a.AssetId
        WHERE d.DepreciationDate <= @CurrentDate
        AND a.DepreciationValue > 1;";  // Select assets where the depreciation date has arrived

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                var assetsToUpdate = await connection.QueryAsync(query, new { CurrentDate = DateTime.Now });

                foreach (var asset in assetsToUpdate)
                {
                    // Update the asset's depreciation value in the asset_item_tb table
                    const string updateQuery = @"
                UPDATE asset_item_tb 
                SET DepreciationValue = @DepreciationValue 
                WHERE AssetId = @AssetId";

                    await connection.ExecuteAsync(updateQuery, new
                    {
                        DepreciationValue = asset.DepreciationValue,
                        AssetId = asset.AssetId
                    });
                }

                return Ok("Depreciation values updated for assets where the depreciation date has passed.");
            }
        }

        [HttpGet("GetAssetByCode")]
        public async Task<IActionResult> GetAssetByCodeAsync(string assetCode)
        {
            const string query = "SELECT * FROM asset_item_tb WHERE AssetCode = @AssetCode";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var asset = await connection.QuerySingleOrDefaultAsync<AssetItem>(query, new { AssetCode = assetCode });

                if (asset == null)
                {
                    return NotFound(new { message = "Asset not found." });
                }

                return Ok(asset); // Return the asset that matches the provided AssetCode
            }
        }


    }
}
