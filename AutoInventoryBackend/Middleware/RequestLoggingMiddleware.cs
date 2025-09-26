using AutoInventoryBackend.Data;
using AutoInventoryBackend.Models;
using System.Diagnostics;

namespace AutoInventoryBackend.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        public RequestLoggingMiddleware(RequestDelegate next) { _next = next; }

        public async Task Invoke(HttpContext ctx, AppDbContext db)
        {
            var sw = Stopwatch.StartNew();
            await _next(ctx);
            sw.Stop();

            try
            {
                var userId = ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var ua = ctx.Request.Headers.UserAgent.ToString();

                db.RequestLogs.Add(new RequestLog
                {
                    AtUtc = DateTime.UtcNow,
                    Method = ctx.Request.Method,
                    Path = ctx.Request.Path,
                    StatusCode = ctx.Response.StatusCode,
                    UserId = userId,
                    IpAddress = ip,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    UserAgent = ua
                });
                await db.SaveChangesAsync();
            }
            catch { /* no romper flujo si el log falla */ }
        }
    }

    public static class RequestLoggingExtensions
    {
        public static IApplicationBuilder UseRequestDbLogging(this IApplicationBuilder app)
            => app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
