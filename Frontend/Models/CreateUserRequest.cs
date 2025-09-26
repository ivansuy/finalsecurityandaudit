using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Frontend.Models
{
    public class CreateUserRequest
    {
        [Required, EmailAddress]
        [JsonPropertyName("email")]
        public string Email { get; set; } = "";

        [Required, MinLength(6)]
        [JsonPropertyName("password")]
        public string Password { get; set; } = "";

        // "Admin" o "SuperAdmin"
        [Required]
        [JsonPropertyName("role")]
        public string Role { get; set; } = "Admin";
    }
}
