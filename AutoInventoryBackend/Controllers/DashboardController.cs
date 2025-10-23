using AutoInventoryBackend.Data;
using AutoInventoryBackend.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoInventoryBackend.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize(Policy = "SuperAdminOnly")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _db;
        public DashboardController(AppDbContext db) { _db = db; }

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

            var logs = await _db.RequestLogs
                .Where(r => r.Path == "/api/auth/login" && r.AtUtc >= since)
                .OrderBy(r => r.IpAddress).ThenBy(r => r.AtUtc)
                .Select(r => new RequestLogSnapshot(
                    r.IpAddress,
                    r.AtUtc,
                    r.StatusCode,
                    r.ElapsedMs,
                    r.UserId))
                .ToListAsync();

            var windows = logs
                .GroupBy(r => new
                {
                    r.IpAddress,
                    WindowStart = new DateTime(r.AtUtc.Year, r.AtUtc.Month, r.AtUtc.Day, r.AtUtc.Hour, r.AtUtc.Minute, 0, DateTimeKind.Utc)
                })
                .Select(g => BuildWindowMetrics(g.Key.IpAddress, g.Key.WindowStart, g.OrderBy(x => x.AtUtc).ToList()))
                .OrderByDescending(x => x.WindowStartUtc)
                .ThenBy(x => x.Ip)
                .ToList();

            return Ok(new
            {
                Minutes = minutes,
                SinceUtc = since,
                GeneratedAtUtc = DateTime.UtcNow,
                Results = windows
            });
        }

        private static LoginMetricsWindowDto BuildWindowMetrics(string ip, DateTime windowStartUtc, List<RequestLogSnapshot> orderedLogs)
        {
            var requestCount = orderedLogs.Count;
            var errorCount = orderedLogs.Count(l => l.StatusCode >= 400);
            var errorRate = requestCount == 0 ? 0 : (double)errorCount / requestCount;

            double? avgInterval = null;
            if (orderedLogs.Count > 1)
            {
                var intervals = new List<double>(orderedLogs.Count - 1);
                for (var i = 1; i < orderedLogs.Count; i++)
                {
                    var seconds = (orderedLogs[i].AtUtc - orderedLogs[i - 1].AtUtc).TotalSeconds;
                    intervals.Add(seconds);
                }
                avgInterval = intervals.Average();
            }

            double? avgElapsed = orderedLogs.Count > 0 ? orderedLogs.Average(l => (double)l.ElapsedMs) : null;
            double? p95Elapsed = orderedLogs.Count > 0 ? CalculatePercentile(orderedLogs.Select(l => (double)l.ElapsedMs), 0.95) : null;

            var statusBreakdown = orderedLogs
                .GroupBy(l => l.StatusCode)
                .ToDictionary(g => (int)g.Key, g => g.Count());

            var uniqueUsers = orderedLogs
                .Select(l => l.UserId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .Count();

            return new LoginMetricsWindowDto
            {
                Ip = ip,
                WindowStartUtc = windowStartUtc,
                WindowEndUtc = windowStartUtc.AddMinutes(1),
                RequestCount = requestCount,
                ErrorCount = errorCount,
                ErrorRate = errorRate,
                AvgSecondsBetweenRequests = avgInterval,
                AvgElapsedMs = avgElapsed,
                P95ElapsedMs = p95Elapsed,
                UniqueUserCount = uniqueUsers,
                StatusBreakdown = statusBreakdown,
                LastStatusCode = orderedLogs.Last().StatusCode,
                FirstRequestAtUtc = orderedLogs.First().AtUtc,
                LastRequestAtUtc = orderedLogs.Last().AtUtc
            };
        }

        private static double CalculatePercentile(IEnumerable<double> values, double percentile)
        {
            var list = values.OrderBy(v => v).ToList();
            if (list.Count == 0) return 0;

            var position = percentile * (list.Count - 1);
            var lowerIndex = (int)Math.Floor(position);
            var upperIndex = (int)Math.Ceiling(position);

            if (lowerIndex == upperIndex) return list[lowerIndex];

            var weight = position - lowerIndex;
            return list[lowerIndex] + weight * (list[upperIndex] - list[lowerIndex]);
        }

        private record RequestLogSnapshot(string IpAddress, DateTime AtUtc, int StatusCode, long ElapsedMs, string? UserId);
    }
}
