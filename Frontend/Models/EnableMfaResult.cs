namespace Frontend.Models
{
    // Respuesta de POST /api/auth/enable-mfa
    public class EnableMfaResult
    {
        public string ManualKey { get; set; } = "";
        public string OtpauthUri { get; set; } = "";
    }
}
