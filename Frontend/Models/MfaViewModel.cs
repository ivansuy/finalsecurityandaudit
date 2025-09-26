using System.ComponentModel.DataAnnotations;

namespace Frontend.Models
{
    // Para la pantalla de MFA durante el LOGIN
    public class MfaViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, MinLength(6), MaxLength(6)]
        public string OtpCode { get; set; } = string.Empty;
    }
}
