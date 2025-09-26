using AutoInventoryBackend.Data;
using AutoInventoryBackend.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AutoInventoryBackend.Services
{
    public interface ILoginBackoffService
    {
        Task<(TimeSpan delay, int failCount, bool blocked)> RegisterAttemptAsync(string key, bool success, string ip, string? user, string reason);
        static string BuildKey(string ip, string? username) => $"{ip}|{(username ?? "-")}".ToLowerInvariant();
    }

    public class LoginBackoffService : ILoginBackoffService
    {
        private readonly IMemoryCache _cache;
        private readonly AppDbContext _db;

        private const int BlockThreshold = 8;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan BlockTime = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

        public LoginBackoffService(IMemoryCache cache, AppDbContext db)
        {
            _cache = cache; _db = db;
        }

        public async Task<(TimeSpan, int, bool)> RegisterAttemptAsync(string key, bool success, string ip, string? user, string reason)
        {
            var now = DateTime.UtcNow;
            var state = _cache.GetOrCreate(key, e =>
            {
                e.AbsoluteExpirationRelativeToNow = Window;
                return new State();
            })!;

            if (state.BlockedUntilUtc.HasValue && now < state.BlockedUntilUtc.Value)
            {
                await Log(now, ip, user, false, state.FailCount, 0, "Blocked");
                return (TimeSpan.Zero, state.FailCount, true);
            }

            if (success)
            {
                state.FailCount = 0;
                state.BlockedUntilUtc = null;
                await Log(now, ip, user, true, 0, 0, reason);
                return (TimeSpan.Zero, 0, false);
            }

            state.FailCount++;
            var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, state.FailCount), MaxBackoff.TotalSeconds));
            if (state.FailCount >= BlockThreshold)
                state.BlockedUntilUtc = now.Add(BlockTime);

            await Log(now, ip, user, false, state.FailCount, (int)delay.TotalSeconds, reason);
            return (delay, state.FailCount, state.BlockedUntilUtc.HasValue && now < state.BlockedUntilUtc.Value);
        }

        private async Task Log(DateTime at, string ip, string? user, bool ok, int count, int backoffSec, string reason)
        {
            _db.AuthAttemptLogs.Add(new AuthAttemptLog
            {
                AttemptAtUtc = at,
                IpAddress = ip,
                Username = user,
                Success = ok,
                FailCountForKey = count,
                BackoffSecondsApplied = backoffSec,
                Reason = reason
            });
            await _db.SaveChangesAsync();
        }

        private class State
        {
            public int FailCount { get; set; }
            public DateTime? BlockedUntilUtc { get; set; }
        }
    }
}
