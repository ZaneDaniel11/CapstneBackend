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
    public class AssetNotificationApiController : ControllerBase
    {

  private readonly string _connectionString = "Data Source=capstone.db";

       [HttpGet("ViewAllNotifications")]
            public async Task<IActionResult> ViewAllNotificationsAsync()
            {
                const string query = @"
                SELECT Id, Type, AssetId, AssetName, AssetCode, Message, Date, 
                    Priority, Read, CategoryId
                FROM AssetNotifications_tb
                ORDER BY Date DESC";

                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    var notifications = await connection.QueryAsync(query);

                    if (!notifications.Any())
                    {
                        return NotFound("No notifications found.");
                    }

                    return Ok(notifications);
                }
            }
    }
}