using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.Sqlite;
using Backend.Models;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersApiController : ControllerBase
    {
        private SqliteConnection _connection = new SqliteConnection("Data Source=capstone.db");

        [HttpPost("CreateUser")]
        public async Task<IActionResult> CreateUserAsync(User user)
        {
            byte[] salt;
            byte[] passwordHash = CreatePasswordHash(user.Password, out salt);

            const string query = @"
        INSERT INTO users_tb (UserName, Password, Salt, CreatedDate, UserType, Email, Token, Name, Department)
        VALUES (@UserName, @Password, @Salt, @CreatedDate, @UserType, @Email, @Token, @Name, @Department);
        SELECT * FROM users_tb ORDER BY UserId DESC LIMIT 1;";

            var result = await _connection.QuerySingleOrDefaultAsync<User>(query, new
            {
                user.UserName,
                Password = Convert.ToBase64String(passwordHash),
                Salt = Convert.ToBase64String(salt),
                user.CreatedDate,
                user.UserType,
                user.Email,
                user.Token,
                user.Name,        // Include Name
                user.Department   // Include Department
            });

            return Ok(result);
        }


        private byte[] CreatePasswordHash(string password, out byte[] salt)
        {
            using (var hmac = new HMACSHA512())
            {
                salt = hmac.Key;  // Generate the salt using the key
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(password));  // Return the hash as byte[]
            }
        }

        [HttpPut("UpdateUser")]
        public async Task<IActionResult> UpdateUserAsync(int UserId, User user)
        {
            byte[] salt;
            byte[] passwordHash = CreatePasswordHash(user.Password, out salt);

            const string query = @"
        UPDATE users_tb 
        SET UserName = @UserName, Password = @Password, Salt = @Salt, 
            UserType = @UserType, Email = @Email, Token = @Token, 
            Name = @Name, Department = @Department
        WHERE UserId = @UserId;
        SELECT * FROM users_tb WHERE UserId = @UserId LIMIT 1;";

            var result = await _connection.QuerySingleOrDefaultAsync<User>(query, new
            {
                user.UserName,
                Password = Convert.ToBase64String(passwordHash),
                Salt = Convert.ToBase64String(salt),
                user.UserType,
                user.Email,
                user.Token,
                user.Name,        // Include Name
                user.Department,   // Include Department
                UserId
            });

            return Ok(result);
        }

        [HttpDelete("DeleteUser")]
        public async Task<IActionResult> DeleteUserAsync(int UserId)
        {
            const string query = "DELETE FROM users_tb WHERE UserId = @UserId;";
            await _connection.ExecuteAsync(query, new { UserId });

            return Ok(new { success = true });
        }

        [HttpGet("GetUsers")]
        public async Task<IActionResult> GetUsersAsync()
        {
            const string query = "SELECT * FROM users_tb;";
            var users = await _connection.QueryAsync<User>(query);
            return Ok(users);
        }
        [HttpGet("GetUserEmail")]
        public async Task<IActionResult> GetUserEmailAsync(int userId)
        {
            const string query = "SELECT Email FROM users_tb WHERE UserId = @UserId;";
            var email = await _connection.QuerySingleOrDefaultAsync<string>(query, new { UserId = userId });

            if (email == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(new { Email = email });
        }

    }
}
