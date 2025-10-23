namespace Frontend.Models
{
    public class DashboardViewModel
    {
        public DashboardSummary? Summary { get; set; }
        public MlDetectionDashboard? Detection { get; set; }
    }

    public class MlDetectionDashboard
    {
        public DateTime GeneratedAtUtc { get; set; }
        public int WindowMinutes { get; set; }
        public double Threshold { get; set; }
        public MlDetectionSummary Summary { get; set; } = new();
        public List<SuspiciousIpSummary> TopSuspiciousIps { get; set; } = new();
        public List<MlDetectionResult> RecentDetections { get; set; } = new();
        public List<MlDetectionResult> LatestByIp { get; set; } = new();
    }

    public class MlDetectionSummary
    {
        public int TotalEvaluations { get; set; }
        public int TotalAnomalies { get; set; }
        public int TotalNormals { get; set; }
        public double AnomalyRate { get; set; }
        public int UniqueIpCount { get; set; }
    }

    public class SuspiciousIpSummary
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

    public class MlDetectionResult
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
}
