using AutoInventoryBackend.Data;
using AutoInventoryBackend.DTOs;
using AutoInventoryBackend.Services;
using AutoInventoryBackend.Services.AnomalyDetection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AutoInventoryBackend.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly LoginMetricsService _loginMetricsService;
        private readonly LoginAnomalyDetectionOptions _anomalyOptions;

        public DashboardController(AppDbContext db, LoginMetricsService loginMetricsService, IOptions<LoginAnomalyDetectionOptions> anomalyOptions)
        {
            _db = db;
            _loginMetricsService = loginMetricsService;
            _anomalyOptions = anomalyOptions.Value;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            var since = DateTime.UtcNow.AddHours(-24);

            var authOk = await _db.AuthAttemptLogs.CountAsync(a => a.AttemptAtUtc >= since && a.Success);
            var authFail = await _db.AuthAttemptLogs.CountAsync(a => a.AttemptAtUtc >= since && !a.Success);

            var topEndpoints = await _db.RequestLogs
                .Where(r => r.AtUtc >= since)
                .GroupBy(r => r.Path)
                .Select(g => new { Endpoint = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            return Ok(new
            {
                WindowHours = 24,
                AuthSuccess = authOk,
                AuthFailed = authFail,
                TopEndpoints = topEndpoints
            });
        }

        [HttpGet("login-metrics")]
        public async Task<IActionResult> LoginMetrics([FromQuery] int minutes = 60)
        {
            if (minutes <= 0) minutes = 60;
            if (minutes > 1440) minutes = 1440;

            var since = DateTime.UtcNow.AddMinutes(-minutes);

            var windows = await _loginMetricsService.GetLoginWindowsAsync(since, DateTime.UtcNow, HttpContext.RequestAborted);

            return Ok(new
            {
                Minutes = minutes,
                SinceUtc = since,
                GeneratedAtUtc = DateTime.UtcNow,
                Results = windows
            });
        }

        [HttpGet("anomalies")]
        public async Task<IActionResult> LoginAnomalies([FromQuery] int hours = 24, [FromQuery] int maxRecords = 150)
        {
            if (hours <= 0) hours = 24;
            if (hours > 168) hours = 168;
            if (maxRecords <= 0) maxRecords = 150;
            if (maxRecords > 500) maxRecords = 500;

            var since = DateTime.UtcNow.AddHours(-hours);

            var detectionsQuery = _db.LoginAnomalyDetections
                .Where(d => d.WindowStartUtc >= since)
                .OrderByDescending(d => d.WindowStartUtc)
                .ThenByDescending(d => d.Score)
                .Take(maxRecords);

            var detections = await detectionsQuery.ToListAsync();

            var detectionDtos = detections
                .Select(d => new LoginAnomalyDetectionDto
                {
                    IpAddress = d.IpAddress,
                    WindowStartUtc = d.WindowStartUtc,
                    WindowEndUtc = d.WindowEndUtc,
                    DetectedAtUtc = d.DetectedAtUtc,
                    Score = d.Score,
                    IsAnomaly = d.IsAnomaly,
                    RequestCount = d.RequestCount,
                    ErrorCount = d.ErrorCount,
                    ErrorRate = d.ErrorRate,
                    AvgSecondsBetweenRequests = d.AvgSecondsBetweenRequests,
                    AvgElapsedMs = d.AvgElapsedMs,
                    P95ElapsedMs = d.P95ElapsedMs,
                    UniqueUserCount = d.UniqueUserCount,
                    LastStatusCode = d.LastStatusCode,
                    SuccessCount = d.SuccessCount,
                    UnauthorizedCount = d.UnauthorizedCount,
                    ServerErrorCount = d.ServerErrorCount
                })
                .ToList();

            var summary = new LoginAnomalyDashboardSummaryDto
            {
                TotalEvaluations = detectionDtos.Count,
                TotalAnomalies = detectionDtos.Count(d => d.IsAnomaly),
                TotalNormals = detectionDtos.Count(d => !d.IsAnomaly),
                AnomalyRate = detectionDtos.Count == 0 ? 0 : (double)detectionDtos.Count(d => d.IsAnomaly) / detectionDtos.Count,
                UniqueIpCount = detectionDtos.Select(d => d.IpAddress).Distinct().Count()
            };

            var topSuspicious = detectionDtos
                .Where(d => d.IsAnomaly)
                .GroupBy(d => d.IpAddress)
                .Select(g => new SuspiciousIpSummaryDto
                {
                    IpAddress = g.Key,
                    LastScore = g.OrderByDescending(x => x.Score).First().Score,
                    LastDetectedAtUtc = g.Max(x => x.DetectedAtUtc),
                    WindowStartUtc = g.OrderByDescending(x => x.WindowStartUtc).First().WindowStartUtc,
                    TotalAnomalies = g.Count(),
                    TotalWindows = detectionDtos.Count(x => x.IpAddress == g.Key),
                    AverageRequestCount = g.Average(x => x.RequestCount),
                    AverageErrorRate = g.Average(x => x.ErrorRate),
                    RecentRequestCount = g.OrderByDescending(x => x.WindowStartUtc).First().RequestCount,
                    RecentErrorRate = g.OrderByDescending(x => x.WindowStartUtc).First().ErrorRate
                })
                .OrderByDescending(x => x.LastScore)
                .ThenByDescending(x => x.TotalAnomalies)
                .Take(10)
                .ToList();

            var latestByIp = detectionDtos
                .GroupBy(d => d.IpAddress)
                .Select(g => g.OrderByDescending(x => x.WindowStartUtc).ThenByDescending(x => x.Score).First())
                .OrderByDescending(d => d.Score)
                .ToList();

            var dto = new LoginAnomalyDashboardDto
            {
                GeneratedAtUtc = DateTime.UtcNow,
                WindowMinutes = Math.Max(1, _anomalyOptions.EvaluationWindowMinutes),
                Threshold = _anomalyOptions.Threshold,
                Summary = summary,
                TopSuspiciousIps = topSuspicious,
                RecentDetections = detectionDtos.OrderByDescending(d => d.DetectedAtUtc).Take(50).ToList(),
                LatestByIp = latestByIp
            };

            return Ok(dto);
        }
    }
}
