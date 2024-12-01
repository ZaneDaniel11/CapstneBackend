using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;
using System.Collections.Generic;
using Events.Models;
using System;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsApiController : ControllerBase
    {
        private readonly string _connectionString = "Data Source=capstone.db";

        // GET: api/EventsApi/GetEvents
        [HttpGet("GetEvents")]
        public async Task<IActionResult> GetEventsAsync()
        {
            const string query = "SELECT * FROM Events_tb";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var events = await connection.QueryAsync<Event>(query);
                return Ok(events);
            }
        }

        // POST: api/EventsApi/AddEvent
        [HttpPost("AddEvent")]
        public async Task<IActionResult> AddEventAsync([FromBody] Event newEvent)
        {
            const string insertQuery = @"
        INSERT INTO Events_tb (Subject, Description, StartTime, EndTime, IsAllDay, RecurrenceRule, Location, Organizer, Color, CreatedAt, UpdatedAt) 
        VALUES (@Subject, @Description, @StartTime, @EndTime, @IsAllDay, @RecurrenceRule, @Location, @Organizer, @Color, @CreatedAt, @UpdatedAt);
        SELECT last_insert_rowid();";

            using (var connection = new SqliteConnection(_connectionString))
            {
                newEvent.CreatedAt = DateTime.UtcNow;
                newEvent.UpdatedAt = DateTime.UtcNow;

                var eventId = await connection.ExecuteScalarAsync<int>(insertQuery, new
                {
                    newEvent.Subject,
                    newEvent.Description,
                    newEvent.StartTime,
                    newEvent.EndTime,
                    IsAllDay = newEvent.IsAllDay ? 1 : 0,
                    newEvent.RecurrenceRule,
                    newEvent.Location,
                    newEvent.Organizer,
                    newEvent.Color,
                    newEvent.CreatedAt,
                    newEvent.UpdatedAt
                });

                newEvent.EventID = eventId;
                return CreatedAtAction(nameof(GetEventsAsync), new { id = eventId }, newEvent);
            }
        }


        // PUT: api/EventsApi/UpdateEvent/{id}
        [HttpPut("UpdateEvent/{id}")]
        public async Task<IActionResult> UpdateEventAsync(int id, [FromBody] Event updatedEvent)
        {
            const string updateQuery = @"
                UPDATE Events_tb 
                SET Subject = @Subject, 
                    Description = @Description, 
                    StartTime = @StartTime, 
                    EndTime = @EndTime, 
                    IsAllDay = @IsAllDay, 
                    RecurrenceRule = @RecurrenceRule, 
                    Location = @Location, 
                    Organizer = @Organizer, 
                    Color = @Color, 
                    UpdatedAt = @UpdatedAt 
                WHERE EventID = @EventID";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                updatedEvent.EventID = id;
                updatedEvent.UpdatedAt = DateTime.UtcNow;

                var affectedRows = await connection.ExecuteAsync(updateQuery, new
                {
                    updatedEvent.Subject,
                    updatedEvent.Description,
                    updatedEvent.StartTime,
                    updatedEvent.EndTime,
                    IsAllDay = updatedEvent.IsAllDay ? 1 : 0,
                    updatedEvent.RecurrenceRule,
                    updatedEvent.Location,
                    updatedEvent.Organizer,
                    updatedEvent.Color,
                    updatedEvent.UpdatedAt,
                    updatedEvent.EventID
                });

                if (affectedRows == 0)
                    return NotFound();  // No record was found to update

                return NoContent();
            }
        }

        // DELETE: api/EventsApi/DeleteEvent/{id}
        [HttpDelete("DeleteEvent/{id}")]
        public async Task<IActionResult> DeleteEventAsync(int id)
        {
            const string deleteQuery = "DELETE FROM Events_tb WHERE EventID = @EventID";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var affectedRows = await connection.ExecuteAsync(deleteQuery, new { EventID = id });

                if (affectedRows == 0)
                    return NotFound();  // No record was found to delete

                return NoContent();
            }
        }
    }
}
