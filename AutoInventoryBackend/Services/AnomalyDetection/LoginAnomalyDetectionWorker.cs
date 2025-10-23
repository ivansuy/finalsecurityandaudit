using AutoInventoryBackend.Data;
using AutoInventoryBackend.DTOs;
using AutoInventoryBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AutoInventoryBackend.Services.AnomalyDetection
{
    public class LoginAnomalyDetectionWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LoginAnomalyDetectionWorker> _logger;
        private readonly LoginAnomalyDetectionOptions _options;
        private IsolationForestModel? _model;
        private DateTime _modelTrainedAtUtc = DateTime.MinValue;
        private DateTime _lastProcessedWindowUtc = DateTime.MinValue;

        public LoginAnomalyDetectionWorker(IServiceScopeFactory scopeFactory, ILogger<LoginAnomalyDetectionWorker> logger, IOptions<LoginAnomalyDetectionOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var metricsService = scope.ServiceProvider.GetRequiredService<LoginMetricsService>();

                    var modelReady = await EnsureModelAsync(metricsService, stoppingToken);
                    if (!modelReady)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        continue;
                    }

                    await EnsureLastProcessedWindowAsync(db, stoppingToken);
                    await EvaluateNewWindowsAsync(db, metricsService, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al ejecutar la detección de anomalías de login");
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        private async Task<bool> EnsureModelAsync(LoginMetricsService metricsService, CancellationToken cancellationToken)
        {
            var retrainInterval = TimeSpan.FromMinutes(Math.Max(1, _options.RetrainMinutes));
            var shouldRetrain = _model == null || (DateTime.UtcNow - _modelTrainedAtUtc) >= retrainInterval;
            if (!shouldRetrain)
            {
                return true;
            }

            var windowMinutes = Math.Max(1, _options.EvaluationWindowMinutes);
            var end = FloorToMinute(DateTime.UtcNow.AddMinutes(-windowMinutes));
            var start = end.AddHours(-Math.Max(1, _options.TrainingLookbackHours));

            var windows = await metricsService.GetLoginWindowsAsync(start, end, cancellationToken);
            var dataset = windows
                .Select(BuildFeatureVector)
                .Where(v => v != null)
                .Select(v => v!)
                .ToList();

            if (dataset.Count < Math.Max(2, _options.MinTrainingSamples))
            {
                if (_model == null)
                {
                    _logger.LogWarning("No hay suficientes muestras para entrenar Isolation Forest. Muestras: {Samples}", dataset.Count);
                }
                return false;
            }

            var sampleSize = Math.Min(_options.SampleSize, dataset.Count);
            var model = IsolationForestTrainer.Train(dataset, _options.Trees, sampleSize, _options.RandomSeed);
            if (model == null)
            {
                _logger.LogWarning("No se pudo entrenar el modelo Isolation Forest (datos insuficientes)");
                return false;
            }

            _model = model;
            _modelTrainedAtUtc = DateTime.UtcNow;
            _logger.LogInformation("Modelo Isolation Forest entrenado ({Samples} muestras, {Trees} árboles)", dataset.Count, _options.Trees);
            return true;
        }

        private async Task EnsureLastProcessedWindowAsync(AppDbContext db, CancellationToken cancellationToken)
        {
            if (_lastProcessedWindowUtc != DateTime.MinValue)
            {
                return;
            }

            var latest = await db.LoginAnomalyDetections
                .OrderByDescending(x => x.WindowStartUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (latest != null)
            {
                _lastProcessedWindowUtc = latest.WindowStartUtc;
            }
        }

        private async Task EvaluateNewWindowsAsync(AppDbContext db, LoginMetricsService metricsService, CancellationToken cancellationToken)
        {
            if (_model == null)
            {
                return;
            }

            var windowMinutes = Math.Max(1, _options.EvaluationWindowMinutes);
            var windowSize = TimeSpan.FromMinutes(windowMinutes);
            var targetWindow = FloorToMinute(DateTime.UtcNow - windowSize);

            if (_lastProcessedWindowUtc != DateTime.MinValue && _lastProcessedWindowUtc >= targetWindow)
            {
                return;
            }

            var catchUpMinutes = Math.Max(windowMinutes, _options.CatchUpMinutes);
            var firstWindow = _lastProcessedWindowUtc == DateTime.MinValue
                ? targetWindow.AddMinutes(-catchUpMinutes)
                : _lastProcessedWindowUtc.AddMinutes(windowMinutes);

            if (firstWindow < targetWindow.AddMinutes(-catchUpMinutes))
            {
                firstWindow = targetWindow.AddMinutes(-catchUpMinutes);
            }

            for (var windowStart = firstWindow; windowStart <= targetWindow; windowStart = windowStart.Add(windowSize))
            {
                var windowEnd = windowStart.Add(windowSize);
                var windows = await metricsService.GetLoginWindowsAsync(windowStart, windowEnd, cancellationToken);
                if (windows.Count == 0)
                {
                    _lastProcessedWindowUtc = windowStart;
                    continue;
                }

                var existingIps = await db.LoginAnomalyDetections
                    .Where(x => x.WindowStartUtc == windowStart)
                    .Select(x => x.IpAddress)
                    .ToListAsync(cancellationToken);
                var existingSet = existingIps.Count > 0 ? new HashSet<string>(existingIps) : null;

                var records = new List<LoginAnomalyDetection>();
                foreach (var window in windows)
                {
                    if (existingSet != null && existingSet.Contains(window.Ip))
                    {
                        continue;
                    }

                    var vector = BuildFeatureVector(window);
                    if (vector == null)
                    {
                        continue;
                    }

                    var score = _model.Score(vector);
                    var isAnomaly = score >= _options.Threshold;

                    window.StatusBreakdown.TryGetValue(200, out var success);
                    window.StatusBreakdown.TryGetValue(401, out var unauthorized);
                    var serverErrors = window.StatusBreakdown.Where(kvp => kvp.Key >= 500).Sum(kvp => kvp.Value);

                    records.Add(new LoginAnomalyDetection
                    {
                        IpAddress = window.Ip,
                        WindowStartUtc = window.WindowStartUtc,
                        WindowEndUtc = window.WindowEndUtc,
                        DetectedAtUtc = DateTime.UtcNow,
                        Score = score,
                        IsAnomaly = isAnomaly,
                        RequestCount = window.RequestCount,
                        ErrorCount = window.ErrorCount,
                        ErrorRate = window.ErrorRate,
                        AvgSecondsBetweenRequests = window.AvgSecondsBetweenRequests,
                        AvgElapsedMs = window.AvgElapsedMs,
                        P95ElapsedMs = window.P95ElapsedMs,
                        UniqueUserCount = window.UniqueUserCount,
                        LastStatusCode = window.LastStatusCode,
                        SuccessCount = success,
                        UnauthorizedCount = unauthorized,
                        ServerErrorCount = serverErrors
                    });
                }

                if (records.Count > 0)
                {
                    await db.LoginAnomalyDetections.AddRangeAsync(records, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                }

                _lastProcessedWindowUtc = windowStart;
            }
        }

        private static double[]? BuildFeatureVector(LoginMetricsWindowDto window)
        {
            if (window.RequestCount <= 0)
            {
                return null;
            }

            window.StatusBreakdown.TryGetValue(200, out var success);
            window.StatusBreakdown.TryGetValue(401, out var unauthorized);
            var serverErrors = window.StatusBreakdown.Where(kvp => kvp.Key >= 500).Sum(kvp => kvp.Value);

            static double SafeLog(double value) => Math.Log(value + 1);

            return new[]
            {
                SafeLog(window.RequestCount),
                SafeLog(window.ErrorCount),
                window.ErrorRate,
                (window.AvgSecondsBetweenRequests ?? 60d) / 60d,
                (window.AvgElapsedMs ?? 0d) / 1000d,
                (window.P95ElapsedMs ?? 0d) / 1000d,
                window.UniqueUserCount,
                SafeLog(success),
                SafeLog(unauthorized),
                SafeLog(serverErrors),
                window.LastStatusCode / 100d
            };
        }

        private static DateTime FloorToMinute(DateTime value)
        {
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Utc);
        }
    }
}
