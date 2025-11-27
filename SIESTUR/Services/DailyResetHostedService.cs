// Services/DailyResetHostedService.cs
using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Siestur.Services;

public class DailyResetHostedService : BackgroundService
{
    private readonly ILogger<DailyResetHostedService> _logger;
    private readonly IServiceProvider _sp;
    private readonly TimeZoneInfo _tz;
    private readonly TimeSpan _runAt;

    public DailyResetHostedService(ILogger<DailyResetHostedService> logger, IServiceProvider sp)
    {
        _logger = logger; _sp = sp;
        // TZ y hora de ejecución configurables por ENV
        var tzId = Environment.GetEnvironmentVariable("Siestur__Timezone") ?? "America/Panama";
        _tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        // Por defecto 23:59
        var hhmm = Environment.GetEnvironmentVariable("Siestur__DailyResetAt") ?? "23:59";
        _runAt = TimeSpan.ParseExact(hhmm, "hh\\:mm", CultureInfo.InvariantCulture);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyResetHostedService iniciado. Hora programada: {RunAt} ({Tz})", _runAt, _tz.Id);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, _tz);
                var todayRun = nowLocal.Date + _runAt;
                var next = (nowLocal <= todayRun) ? todayRun : todayRun.AddDays(1);

                var delay = next - nowLocal;
                await Task.Delay(delay, stoppingToken);

                using var scope = _sp.CreateScope();
                var reset = scope.ServiceProvider.GetRequiredService<IDayResetService>();
                var (archived, start) = await reset.ResetTodayAsync();
                _logger.LogInformation("Reset diario ejecutado. Archivados: {Archived}. Start: {Start}.", archived, start);

                // Espera 60s para evitar segundo disparo en el mismo minuto
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en DailyResetHostedService");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
