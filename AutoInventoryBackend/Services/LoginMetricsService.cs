using AutoInventoryBackend.Data;
using AutoInventoryBackend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AutoInventoryBackend.Services
{
    public class LoginMetricsService
    {
        private readonly AppDbContext _db;

        public LoginMetricsService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<LoginMetricsWindowDto>> GetLoginWindowsAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
        {
            if (endUtc <= startUtc)
            {
                return new();
            }

            var logs = await _db.RequestLogs
                .Where(r => r.Path == "/api/auth/login" && r.AtUtc >= startUtc && r.AtUtc < endUtc)
                .OrderBy(r => r.IpAddress).ThenBy(r => r.AtUtc)
                .Select(r => new RequestLogSnapshot(
                    r.IpAddress,
                    DateTime.SpecifyKind(r.AtUtc, DateTimeKind.Utc),
                    r.StatusCode,
                    r.ElapsedMs,
                    r.UserId))
                .ToListAsync(cancellationToken);

            if (logs.Count == 0)
            {
                return new();
            }

            return logs
                .GroupBy(r => new
                {
                    r.IpAddress,
                    WindowStart = FloorToMinute(r.AtUtc)
                })
                .Select(g => BuildWindowMetrics(g.Key.IpAddress, g.Key.WindowStart, g.OrderBy(x => x.AtUtc).ToList()))
                .OrderByDescending(x => x.WindowStartUtc)
                .ThenBy(x => x.Ip)
                .ToList();
        }

        public Task<List<LoginMetricsWindowDto>> GetRecentWindowsAsync(int minutes, CancellationToken cancellationToken)
        {
            if (minutes <= 0)
            {
                minutes = 60;
            }
            else if (minutes > 1440)
            {
                minutes = 1440;
            }

            var endUtc = DateTime.UtcNow;
            var startUtc = endUtc.AddMinutes(-minutes);
            return GetLoginWindowsAsync(startUtc, endUtc, cancellationToken);
        }

        internal static LoginMetricsWindowDto BuildWindowMetrics(string ip, DateTime windowStartUtc, List<RequestLogSnapshot> orderedLogs)
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
                WindowStartUtc = DateTime.SpecifyKind(windowStartUtc, DateTimeKind.Utc),
                WindowEndUtc = DateTime.SpecifyKind(windowStartUtc.AddMinutes(1), DateTimeKind.Utc),
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

        private static DateTime FloorToMinute(DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Utc);
        }

        private record RequestLogSnapshot(string IpAddress, DateTime AtUtc, int StatusCode, long ElapsedMs, string? UserId);
    }
}
