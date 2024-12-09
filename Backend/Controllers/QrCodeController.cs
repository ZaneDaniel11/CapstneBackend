using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading.Tasks;
using QRCoder;
using System;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/qrcode")]
    public class QrCodeController : ControllerBase
    {
        private readonly string _connectionString = "Data Source=capstone.db;";

        [HttpGet("GetUsers")]
        public async Task<IActionResult> GetUsersAsync()
        {
            const string query = "SELECT Name, LastName FROM Users";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var users = await connection.QueryAsync<User1>(query);

                var usersWithQr = new List<object>();
                foreach (var user in users)
                {
                    string qrData = $"{user.Name} {user.LastName}";

                    // Generate QR Code
                    using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                    {
                        QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
                        PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
                        string qrBase64 = Convert.ToBase64String(qrCode.GetGraphic(20));

                        usersWithQr.Add(new
                        {
                            user.Name,
                            user.LastName,
                            QrCodeData = qrBase64
                        });
                    }
                }

                return Ok(usersWithQr);
            }
        }

        // POST: api/qrcode/AddUser
        [HttpPost("AddUser")]
        public async Task<IActionResult> InsertUserAsync([FromBody] User1 user)
        {
            if (user == null || string.IsNullOrEmpty(user.Name) || string.IsNullOrEmpty(user.LastName))
            {
                return BadRequest("Invalid data");
            }

            const string insertQuery = "INSERT INTO Users (Name, LastName) VALUES (@Name, @LastName); SELECT last_insert_rowid();";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                int userId = await connection.ExecuteScalarAsync<int>(insertQuery, user);

                return CreatedAtAction(nameof(GetUsersAsync), new { id = userId }, user);
            }
        }

        // DELETE: api/qrcode/DeleteUser/{id}
        [HttpDelete("DeleteUser/{id}")]
        public async Task<IActionResult> DeleteUserAsync(int id)
        {
            const string deleteQuery = "DELETE FROM Users WHERE Id = @Id";

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                int affectedRows = await connection.ExecuteAsync(deleteQuery, new { Id = id });

                if (affectedRows == 0)
                    return NotFound();

                return NoContent();
            }
        }

        public class User1
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string LastName { get; set; }
        }
    }
}
