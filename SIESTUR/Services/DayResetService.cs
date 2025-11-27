// Services/DayResetService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Siestur.Data;
using Siestur.Models;
using Siestur.Services.Hubs;

namespace Siestur.Services;

public class DayResetService : IDayResetService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<TurnsHub> _turnsHub;
    private readonly IHubContext<WindowsHub> _windowsHub;
    private readonly IHubContext<VideosHub> _videosHub;

    public DayResetService(ApplicationDbContext db, IHubContext<TurnsHub> turnsHub, IHubContext<WindowsHub> windowsHub, IHubContext<VideosHub> videosHub)
    {
        _db = db; _turnsHub = turnsHub; _windowsHub = windowsHub; _videosHub = videosHub;
    }

    public async Task<(int turnsArchived, int startDefault)> ResetTodayAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ===== (1) Reinicio de contador =====
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var startDefault = settings?.StartNumberDefault
            ?? int.Parse(Environment.GetEnvironmentVariable("Siestur__StartNumberDefault") ?? "0");

        var dc = await _db.DayCounters.FirstOrDefaultAsync(x => x.ServiceDate == today);
        if (dc is null) _db.DayCounters.Add(new DayCounter { ServiceDate = today, NextNumber = startDefault });
        else dc.NextNumber = startDefault;

        // ===== (2) Cerrar sesiones abiertas =====
        var openSessions = await _db.WorkerSessions.Where(ws => ws.EndedAt == null).ToListAsync();
        foreach (var s in openSessions) s.EndedAt = DateTime.UtcNow;

        // ===== (3) Volcar turnos a TurnFacts + OperatorDailyFacts =====
        var toDelete = await _db.Turns
            .Where(t => DateOnly.FromDateTime(t.CreatedAt) == today)
            .Include(t => t.Window)
            .ToListAsync();

        int? diff(DateTime? a, DateTime? b) => (a.HasValue && b.HasValue) ? (int?)(a.Value - b.Value).TotalSeconds : null;

        var facts = toDelete.Select(t => new TurnFact
        {
            ServiceDate = DateOnly.FromDateTime(t.CreatedAt),
            Number = t.Number,
            Kind = t.Kind ?? "NORMAL",
            FinalStatus = t.Status,
            WindowNumber = t.Window?.Number,
            OperatorUserId = t.CompletedByUserId ?? t.ServedByUserId ?? t.CalledByUserId,
            CreatedAt = t.CreatedAt,
            CalledAt = t.CalledAt,
            ServedAt = t.ServedAt,
            CompletedAt = t.CompletedAt,
            SkippedAt = t.SkippedAt,
            WaitToCallSec = diff(t.CalledAt, t.CreatedAt),
            CallToServeSec = diff(t.ServedAt, t.CalledAt),
            ServeToCompleteSec = diff(t.CompletedAt, t.ServedAt),
            TotalLeadTimeSec = diff(t.CompletedAt ?? t.ServedAt ?? t.CalledAt, t.CreatedAt)
        }).ToList();

        if (facts.Count() > 0) _db.TurnFacts.AddRange(facts);

        if (facts.Any())
        {
            var byOp = facts
                .Where(f => f.OperatorUserId.HasValue)
                .GroupBy(f => f.OperatorUserId!.Value)
                .Select(g => new OperatorDailyFact
                {
                    ServiceDate = today,
                    OperatorUserId = g.Key,
                    ServedCount = g.Count(x => x.FinalStatus == "DONE"),
                    AvgServeToCompleteSec = g.Where(x => x.ServeToCompleteSec.HasValue).Any()
                        ? g.Average(x => (double?)x.ServeToCompleteSec) : null,
                    AvgTotalLeadTimeSec = g.Where(x => x.TotalLeadTimeSec.HasValue).Any()
                        ? g.Average(x => (double?)x.TotalLeadTimeSec) : null,
                    WindowMin = g.Min(x => x.WindowNumber),
                    WindowMax = g.Max(x => x.WindowNumber)
                });
            if (byOp.Any()) _db.OperatorDailyFacts.AddRange(byOp);
        }

        // ===== (4) Borrar turnos del día y purgar histórico > 7 días =====
        _db.Turns.RemoveRange(toDelete);
        var limitDate = today.AddDays(-7);
        var oldFacts = await _db.TurnFacts.Where(f => f.ServiceDate < limitDate).ToListAsync();
        if (oldFacts.Count > 0) _db.TurnFacts.RemoveRange(oldFacts);

        // ===== (5) Guardar marca de último reset =====
        var state = await _db.SystemStates.FirstOrDefaultAsync(s => s.Id == 1) ?? new SystemState();
        state.LastDailyReset = today;
        if (_db.Entry(state).State == EntityState.Detached) _db.SystemStates.Add(state);

        await _db.SaveChangesAsync();

        // ===== (6) Notificar =====
        await _turnsHub.Clients.All.SendAsync("turns:reset");
        await _windowsHub.Clients.All.SendAsync("windows:updated");
        await _videosHub.Clients.All.SendAsync("videos:updated");

        return (facts.Count(), startDefault);
    }
}
