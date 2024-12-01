using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using Items.Models;
using Categorys.Models;
using System.Threading.Tasks;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ItemApiController : ControllerBase
    {
        private readonly string _connectionString = "Data Source=capstone.db";

        // GET: api/ItemApi/GetItemsByCategory?categoryID=1
        [HttpGet("GetItemsByCategory")]
        public async Task<IActionResult> GetItemsByCategoryAsync(int categoryID)
        {
            const string query = "SELECT * FROM items_db WHERE CategoryID = @CategoryID";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var items = await connection.QueryAsync<Item>(query, new { CategoryID = categoryID });
                return Ok(items); // Return filtered items based on CategoryID
            }
        }




        // POST: api/ItemApi/InsertItem
        [HttpPost("InsertItem")]
        public async Task<IActionResult> InsertItemAsync(Item newItem)
        {
            const string query = @"
                INSERT INTO items_db (CategoryID, ItemName, Quantity, Description, DateAdded) 
                VALUES (@CategoryID, @ItemName, @Quantity, @Description, @DateAdded);
                SELECT * FROM items_db ORDER BY ItemID DESC LIMIT 1;";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var result = await connection.QuerySingleOrDefaultAsync<Item>(query, new
                {
                    newItem.CategoryID,
                    newItem.ItemName,
                    newItem.Quantity,
                    newItem.Description,
                    newItem.DateAdded
                });

                return Ok(result);
            }
        }

        // DELETE: api/ItemApi/DeleteItem?ItemID=1
        [HttpDelete("DeleteItem")]
        public async Task<IActionResult> DeleteItemAsync(int ItemID, int CategoryID)
        {
            const string query = "DELETE FROM items_db WHERE ItemID = @ItemID AND CategoryID = @CategoryID";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var result = await connection.ExecuteAsync(query, new { ItemID, CategoryID });
                return Ok(new { success = result > 0 });
            }
        }

        // PUT: api/ItemApi/AddQuantity?ItemID=1&CategoryID=1
        [HttpPut("AddQuantity")]
        public async Task<IActionResult> AddQuantityAsync(int ItemID, int CategoryID, int quantityToAdd)
        {
            const string query = @"
                UPDATE items_db 
                SET Quantity = Quantity + @QuantityToAdd 
                WHERE ItemID = @ItemID AND CategoryID = @CategoryID;
                SELECT * FROM items_db WHERE ItemID = @ItemID AND CategoryID = @CategoryID LIMIT 1;";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var result = await connection.QuerySingleOrDefaultAsync<Item>(query, new
                {
                    QuantityToAdd = quantityToAdd,
                    ItemID,
                    CategoryID
                });

                return Ok(result);
            }
        }

        [HttpPut("UpdateItem")]
        public async Task<IActionResult> UpdateItemAsync(int ItemID, int CategoryID, Item updatedItem)
        {
            const string query = @"
                UPDATE items_db 
                SET 
                    ItemName = @ItemName, 
                    Quantity = @Quantity, 
                    Description = @Description, 
                    DateAdded = @DateAdded
                WHERE ItemID = @ItemID AND CategoryID = @CategoryID;
                SELECT * FROM items_db WHERE ItemID = @ItemID AND CategoryID = @CategoryID LIMIT 1;";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var result = await connection.QuerySingleOrDefaultAsync<Item>(query, new
                {
                    updatedItem.ItemName,
                    updatedItem.Quantity,
                    updatedItem.Description,
                    updatedItem.DateAdded,
                    ItemID,
                    CategoryID
                });

                return Ok(result);
            }
        }
    }
}
