using AutoInventoryBackend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    }
}
