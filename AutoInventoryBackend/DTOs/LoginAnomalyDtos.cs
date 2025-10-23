namespace AutoInventoryBackend.DTOs
{
    public class LoginAnomalyDetectionDto
    {
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

    public class SuspiciousIpSummaryDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public double LastScore { get; set; }
        public DateTime LastDetectedAtUtc { get; set; }
        public DateTime WindowStartUtc { get; set; }
        public int TotalAnomalies { get; set; }
        public int TotalWindows { get; set; }
        public double AverageRequestCount { get; set; }
        public double AverageErrorRate { get; set; }
        public int RecentRequestCount { get; set; }
        public double RecentErrorRate { get; set; }
    }

    public class LoginAnomalyDashboardDto
    {
        public DateTime GeneratedAtUtc { get; set; }
        public int WindowMinutes { get; set; }
        public double Threshold { get; set; }
        public LoginAnomalyDashboardSummaryDto Summary { get; set; } = new();
        public List<SuspiciousIpSummaryDto> TopSuspiciousIps { get; set; } = new();
        public List<LoginAnomalyDetectionDto> RecentDetections { get; set; } = new();
        public List<LoginAnomalyDetectionDto> LatestByIp { get; set; } = new();
    }

    public class LoginAnomalyDashboardSummaryDto
    {
        public int TotalEvaluations { get; set; }
        public int TotalAnomalies { get; set; }
        public int TotalNormals { get; set; }
        public double AnomalyRate { get; set; }
        public int UniqueIpCount { get; set; }
    }
}
