namespace AutoInventoryBackend.Models
{
    public class AuthAttemptLog
    {
        public int Id { get; set; }
        public DateTime AttemptAtUtc { get; set; }
        public string? Username { get; set; }
        public string IpAddress { get; set; } = default!;
        public bool Success { get; set; }
        public int FailCountForKey { get; set; }
        public int BackoffSecondsApplied { get; set; }
        public string? Reason { get; set; }
    }
}
