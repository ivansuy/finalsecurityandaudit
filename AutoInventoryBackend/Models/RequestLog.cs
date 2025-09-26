namespace AutoInventoryBackend.Models
{
    public class RequestLog
    {
        public long Id { get; set; }
        public DateTime AtUtc { get; set; }
        public string Method { get; set; } = default!;
        public string Path { get; set; } = default!;
        public int StatusCode { get; set; }
        public string? UserId { get; set; }
        public string IpAddress { get; set; } = default!;
        public long ElapsedMs { get; set; }
        public string? UserAgent { get; set; }
    }
}
