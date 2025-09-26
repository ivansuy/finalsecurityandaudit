namespace AutoInventoryBackend.DTOs
{
    public class LoginRequestDto
    {
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string? OtpCode { get; set; }
    }

    public class LoginResponseDto
    {
        public bool RequiresMfa { get; set; }
        public string? Token { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public string? Message { get; set; }
    }

    public class CreateUserDto
    {
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string Role { get; set; } = "Admin"; // Admin o SuperAdmin
    }

    public class EnableMfaResponseDto
    {
        public string ManualKey { get; set; } = default!;
        public string OtpauthUri { get; set; } = default!;
    }
}
