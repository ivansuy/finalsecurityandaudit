namespace Frontend.Models
{
    public class UserSummary
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";
        public string UserName { get; set; } = "";
        public bool TwoFactorEnabled { get; set; }
    }
}
