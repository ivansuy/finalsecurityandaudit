using System;
using System.ComponentModel.DataAnnotations;

namespace AutoInventoryBackend.Models
{
    public class LoginAnomalyDetection
    {
        public long Id { get; set; }
        [MaxLength(100)]
        public string IpAddress { get; set; } = string.Empty;
        public DateTime WindowStartUtc { get; set; }
        public DateTime WindowEndUtc { get; set; }
        public DateTime DetectedAtUtc { get; set; }
        public double Score { get; set; }
        public bool IsAnomaly { get; set; }
        public int RequestCount { get; set; }
        public int ErrorCount { get; set; }
        public double ErrorRate { get; set; }
        public double? AvgSecondsBetweenRequests { get; set; }
        public double? AvgElapsedMs { get; set; }
        public double? P95ElapsedMs { get; set; }
        public int UniqueUserCount { get; set; }
        public int LastStatusCode { get; set; }
        public int SuccessCount { get; set; }
        public int UnauthorizedCount { get; set; }
        public int ServerErrorCount { get; set; }
    }
}
