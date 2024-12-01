namespace Backend.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string? UserName { get; set; }  // Nullable
        public string? Password { get; set; }  // Nullable
        public string? Salt { get; set; }  // Nullable
        public DateTime CreatedDate { get; set; }
        public string? UserType { get; set; }  // Nullable
        public string? Email { get; set; }  // Nullable
        public string? Token { get; set; }  // Nullable
          public string Name { get; set; }          // New field
    public string Department { get; set; }  
    }

    public class LoginDto
    {
        public string? UserName { get; set; }  // Nullable
        public string? Password { get; set; }  // Nullable
    }
}
