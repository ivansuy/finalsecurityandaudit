using System;
using System.Collections.Generic;

namespace AutoInventoryBackend.DTOs
{
    public class LoginMetricsWindowDto
    {
        public string Ip { get; set; } = default!;
        public DateTime WindowStartUtc { get; set; }
        public DateTime WindowEndUtc { get; set; }
        public int RequestCount { get; set; }
        public int ErrorCount { get; set; }
        public double ErrorRate { get; set; }
        public double? AvgSecondsBetweenRequests { get; set; }
        public double? AvgElapsedMs { get; set; }
        public double? P95ElapsedMs { get; set; }
        public int UniqueUserCount { get; set; }
        public Dictionary<int, int> StatusBreakdown { get; set; } = new();
        public int LastStatusCode { get; set; }
        public DateTime FirstRequestAtUtc { get; set; }
        public DateTime LastRequestAtUtc { get; set; }
    }
}
