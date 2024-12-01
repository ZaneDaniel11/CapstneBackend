using Microsoft.AspNetCore.Mvc;
using Dapper;
using Backend.Models;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Security.Claims;
using System.Text;
using System.Security.Cryptography;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginApiController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly string _jwtKey;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;

        public LoginApiController(IConfiguration configuration)
        {
            _connectionString = "Data Source=capstone.db";
            _jwtKey = configuration["Jwt:Key"];
            _jwtIssuer = configuration["Jwt:Issuer"];
            _jwtAudience = configuration["Jwt:Audience"];
        }

        [HttpPost]
        [Route("Authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] LoginDto loginUser)
        {
            if (loginUser == null || string.IsNullOrEmpty(loginUser.UserName) || string.IsNullOrEmpty(loginUser.Password))
            {
                return BadRequest(new { message = "Invalid login attempt." });
            }

            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                var query = "SELECT * FROM users_tb WHERE UserName = @UserName";
                var user = await db.QueryFirstOrDefaultAsync<User>(query, new { loginUser.UserName });

                if (user == null)
                {
                    Console.WriteLine("User not found.");
                    return Unauthorized(new { message = "Invalid credentials." });
                }

                if (!VerifyPasswordHash(loginUser.Password, user.Password, user.Salt))
                {
                    Console.WriteLine("Password does not match.");
                    return Unauthorized(new { message = "Invalid credentials." });
                }

                var token = GenerateJwtToken(user);

                user.Token = token;

                return Ok(user);
            }
        }

        private byte[] CreatePasswordHash(string password, out byte[] salt)
        {
            using (var hmac = new HMACSHA512())
            {
                salt = hmac.Key;  // Generate the salt using the key
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(password));  // Return the hash as byte[]
            }
        }

        private bool VerifyPasswordHash(string enteredPassword, string storedHashString, string storedSaltString)
        {
            byte[] storedHash = Convert.FromBase64String(storedHashString);  // Convert back to byte[]
            byte[] storedSalt = Convert.FromBase64String(storedSaltString);  // Convert back to byte[]

            using (var hmac = new HMACSHA512(storedSalt))  // Use the stored salt as the key
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(enteredPassword));
                return computedHash.SequenceEqual(storedHash);  // Compare byte arrays
            }
        }


        private string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.UserType)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                _jwtIssuer,
                _jwtAudience,
                claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
