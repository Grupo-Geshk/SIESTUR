// Controllers/StatsController.cs  (V2 - añade today-list y range-list con filtros/paginación)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siestur.Data;
using Siestur.Models;
using Siestur.Models.Dto;
using System.Globalization;
using System.Text;

namespace Siestur.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")] // Solo admin
public class StatsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public StatsController(ApplicationDbContext db) => _db = db;

    // ==== Helpers internos ===================================================

    private static DateOnly UtcToday() => DateOnly.FromDateTime(DateTime.UtcNow);

    // Proyección común para Turns/TurnFacts (BASE EXISTENTE)
    private class FactRow
    {
        public DateOnly ServiceDate { get; set; }
        public int Number { get; set; }
        public string Kind { get; set; } = "NORMAL";
        public string FinalStatus { get; set; } = "DONE";
        public int? WindowNumber { get; set; }
        public Guid? OperatorUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CalledAt { get; set; }
        public DateTime? ServedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? SkippedAt { get; set; }
        public int? WaitToCallSec { get; set; }
        public int? CallToServeSec { get; set; }
        public int? ServeToCompleteSec { get; set; }
        public int? TotalLeadTimeSec { get; set; }
    }

    // Mapeo Turn -> FactRow (BASE EXISTENTE)
    private static FactRow TurnToRow(Turn t, int? windowNumber, Guid? operatorUserId)
    {
        var final = (t.Status?.ToUpperInvariant()) switch
        {
            "DONE" => "DONE",
            "SKIPPED" => "SKIPPED",
            _ => t.Status?.ToUpperInvariant() ?? "PENDING"
        };

        int? s(DateTime? a, DateTime? b) =>
            (a.HasValue && b.HasValue) ? (int?)(a.Value - b.Value).TotalSeconds : null;

        var totalLead = t.CompletedAt ?? t.ServedAt ?? t.CalledAt;
        return new FactRow
        {
            ServiceDate = DateOnly.FromDateTime(t.CreatedAt.ToUniversalTime()),
            Number = t.Number,
            Kind = t.Kind ?? "NORMAL",
            FinalStatus = final,
            WindowNumber = windowNumber,
            OperatorUserId = t.CompletedByUserId ?? t.ServedByUserId ?? operatorUserId,
            CreatedAt = t.CreatedAt,
            CalledAt = t.CalledAt,
            ServedAt = t.ServedAt,
            CompletedAt = t.CompletedAt,
            SkippedAt = t.SkippedAt,
            WaitToCallSec = s(t.CalledAt, t.CreatedAt),
            CallToServeSec = s(t.ServedAt, t.CalledAt),
            ServeToCompleteSec = s(t.CompletedAt, t.ServedAt),
            TotalLeadTimeSec = s(totalLead, t.CreatedAt)
        };
    }

    private static double? Avg(IEnumerable<int?> xs)
    {
        var vals = xs.Where(x => x.HasValue).Select(x => (double)x!.Value).ToList();
        return vals.Count == 0 ? null : vals.Average();
    }

    private static DateOnly ParseDateOnly(string s) =>
        DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    // Carga TurnFacts + (si aplica) los Turns del día actual (BASE EXISTENTE)
    private async Task<List<FactRow>> GetRows(DateOnly from, DateOnly to)
    {
        var rows = new List<FactRow>();

        var facts = await _db.TurnFacts
            .Where(f => f.ServiceDate >= from && f.ServiceDate <= to)
            .Select(f => new FactRow
            {
                ServiceDate = f.ServiceDate,
                Number = f.Number,
                Kind = f.Kind,
                FinalStatus = f.FinalStatus,
                WindowNumber = f.WindowNumber,
                OperatorUserId = f.OperatorUserId,
                CreatedAt = f.CreatedAt,
                CalledAt = f.CalledAt,
                ServedAt = f.ServedAt,
                CompletedAt = f.CompletedAt,
                SkippedAt = f.SkippedAt,
                WaitToCallSec = f.WaitToCallSec,
                CallToServeSec = f.CallToServeSec,
                ServeToCompleteSec = f.ServeToCompleteSec,
                TotalLeadTimeSec = f.TotalLeadTimeSec
            })
            .ToListAsync();

        rows.AddRange(facts);

        var today = UtcToday();
        if (from <= today && today <= to)
        {
            var turnsToday = await _db.Turns
                .Include(t => t.Window)
                .Where(t => DateOnly.FromDateTime(t.CreatedAt.ToUniversalTime()) == today)
                .ToListAsync();

            foreach (var t in turnsToday)
                rows.Add(TurnToRow(t, t.Window?.Number, t.CompletedByUserId ?? t.ServedByUserId));
        }

        return rows;
    }

    // KPIs/series desde FactRow (BASE EXISTENTE)
    private static StatsResponseDto BuildStats(List<FactRow> data, DateOnly from, DateOnly to)
    {
        var resp = new StatsResponseDto();
        var today = UtcToday();

        // Series por día
        resp.Series = data
            .GroupBy(x => x.ServiceDate)
            .Select(g => new DailyStatsPointDto
            {
                Date = g.Key,
                TotalTurns = g.Count(),
                DoneCount = g.Count(x => x.FinalStatus == "DONE"),
                SkippedCount = g.Count(x => x.FinalStatus == "SKIPPED"),
                AvgWaitToCallSec = Avg(g.Select(x => x.WaitToCallSec)),
                AvgCallToServeSec = Avg(g.Select(x => x.CallToServeSec)),
                AvgServeToCompleteSec = Avg(g.Select(x => x.ServeToCompleteSec)),
                AvgTotalLeadTimeSec = Avg(g.Select(x => x.TotalLeadTimeSec)),
                IsToday = (g.Key == today)
            })
            .OrderBy(x => x.Date)
            .ToList();

        // Summary
        resp.Summary.TotalTurns = data.Count;
        resp.Summary.DoneCount = data.Count(x => x.FinalStatus == "DONE");
        resp.Summary.SkippedCount = data.Count(x => x.FinalStatus == "SKIPPED");
        resp.Summary.DisabilityCount = data.Count(x => x.Kind == "DISABILITY");
        resp.Summary.SpecialCount = data.Count(x => x.Kind == "SPECIAL");

        resp.Summary.AvgWaitToCallSec = Avg(data.Select(x => x.WaitToCallSec));
        resp.Summary.AvgCallToServeSec = Avg(data.Select(x => x.CallToServeSec));
        resp.Summary.AvgServeToCompleteSec = Avg(data.Select(x => x.ServeToCompleteSec));
        resp.Summary.AvgTotalLeadTimeSec = Avg(data.Select(x => x.TotalLeadTimeSec));

        // By operator
        resp.ByOperator = data
            .Where(x => x.OperatorUserId.HasValue)
            .GroupBy(x => x.OperatorUserId!.Value)
            .Select(g => new OperatorStatsDto
            {
                OperatorUserId = g.Key,
                OperatorName = null,
                ServedCount = g.Count(x => x.ServedAt.HasValue || x.FinalStatus == "DONE"),
                AvgServeToCompleteSec = Avg(g.Select(x => x.ServeToCompleteSec)),
                AvgTotalLeadTimeSec = Avg(g.Select(x => x.TotalLeadTimeSec))
            })
            .OrderByDescending(x => x.ServedCount)
            .ThenBy(x => x.AvgServeToCompleteSec ?? double.MaxValue)
            .ToList();

        // By window
        resp.ByWindow = data
            .Where(x => x.WindowNumber.HasValue)
            .GroupBy(x => x.WindowNumber!.Value)
            .Select(g => new WindowStatsDto
            {
                WindowNumber = g.Key,
                ServedCount = g.Count(x => x.ServedAt.HasValue || x.FinalStatus == "DONE"),
                AvgServeToCompleteSec = Avg(g.Select(x => x.ServeToCompleteSec)),
                AvgTotalLeadTimeSec = Avg(g.Select(x => x.TotalLeadTimeSec))
            })
            .OrderByDescending(x => x.ServedCount)
            .ThenBy(x => x.AvgServeToCompleteSec ?? double.MaxValue)
            .ToList();

        return resp;
    }

    // ==== ENDPOINTS KPIs / EXISTENTES ========================================

    [HttpGet("today")]
    public async Task<ActionResult<StatsResponseDto>> Today()
    {
        var d = UtcToday();
        var rows = await GetRows(d, d);
        var dto = BuildStats(rows, d, d);
        await HydrateOperatorNames(dto);
        return Ok(dto);
    }

    [HttpGet("range")]
    public async Task<ActionResult<StatsResponseDto>> Range([FromQuery] string from, [FromQuery] string to)
    {
        var dFrom = ParseDateOnly(from);
        var dTo = ParseDateOnly(to);
        if (dTo < dFrom) return BadRequest("'to' debe ser >= 'from'.");

        var rows = await GetRows(dFrom, dTo);
        var dto = BuildStats(rows, dFrom, dTo);
        await HydrateOperatorNames(dto);
        return Ok(dto);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string from, [FromQuery] string to)
    {
        var dFrom = ParseDateOnly(from);
        var dTo = ParseDateOnly(to);
        if (dTo < dFrom) return BadRequest("'to' debe ser >= 'from'.");

        var rows = await GetRows(dFrom, dTo);

        var sb = new StringBuilder();
        sb.AppendLine("ServiceDate,Number,Kind,FinalStatus,WindowNumber,OperatorUserId,CreatedAt,CalledAt,ServedAt,CompletedAt,SkippedAt,WaitToCallSec,CallToServeSec,ServeToCompleteSec,TotalLeadTimeSec");
        foreach (var r in rows.OrderBy(r => r.ServiceDate).ThenBy(r => r.Number))
        {
            string dt(DateTime? x) => x.HasValue
                ? x.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
                : "";
            sb.AppendLine(string.Join(",", new[]
            {
                r.ServiceDate.ToString("yyyy-MM-dd"),
                r.Number.ToString(),
                r.Kind,
                r.FinalStatus,
                r.WindowNumber?.ToString() ?? "",
                r.OperatorUserId?.ToString() ?? "",
                dt(r.CreatedAt),
                dt(r.CalledAt),
                dt(r.ServedAt),
                dt(r.CompletedAt),
                dt(r.SkippedAt),
                r.WaitToCallSec?.ToString() ?? "",
                r.CallToServeSec?.ToString() ?? "",
                r.ServeToCompleteSec?.ToString() ?? "",
                r.TotalLeadTimeSec?.ToString() ?? ""
            }));
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"siestur_stats_{from}_{to}.csv");
    }

    [HttpGet("paged")]
    public async Task<ActionResult<StatsResponseDto>> Paged([FromQuery] int page = 0, [FromQuery] int pageSize = 1)
    {
        var today = UtcToday();
        var targetStart = today.AddDays(-page);
        var targetEnd = targetStart.AddDays(-(pageSize - 1));
        if (targetEnd < today.AddDays(-7))
            return BadRequest("Solo se permiten hasta 7 días atrás.");

        var rows = await GetRows(targetEnd, targetStart);
        var dto = BuildStats(rows, targetEnd, targetStart);
        await HydrateOperatorNames(dto);
        return Ok(dto);
    }

    [HttpGet("operators/today")]
    public async Task<ActionResult<IEnumerable<OperatorDailyFactDto>>> OperatorsToday()
    {
        var today = UtcToday();
        var facts = await _db.OperatorDailyFacts.Where(f => f.ServiceDate == today).ToListAsync();

        if (facts.Count == 0)
        {
            var rows = await GetRows(today, today);
            var dto = BuildStats(rows, today, today);
            await HydrateOperatorNames(dto);

            return Ok(dto.ByOperator.Select(o => new OperatorDailyFactDto
            {
                ServiceDate = today,
                OperatorUserId = o.OperatorUserId,
                OperatorName = o.OperatorName,
                ServedCount = o.ServedCount,
                AvgServeToCompleteSec = o.AvgServeToCompleteSec,
                AvgTotalLeadTimeSec = o.AvgTotalLeadTimeSec
            }));
        }

        var ids = facts.Select(f => f.OperatorUserId).Distinct().ToList();
        var map = await _db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Email ?? x.Id.ToString());

        return Ok(facts.Select(f => new OperatorDailyFactDto
        {
            ServiceDate = f.ServiceDate,
            OperatorUserId = f.OperatorUserId,
            OperatorName = map.TryGetValue(f.OperatorUserId, out var name) ? name : null,
            ServedCount = f.ServedCount,
            AvgServeToCompleteSec = f.AvgServeToCompleteSec,
            AvgTotalLeadTimeSec = f.AvgTotalLeadTimeSec,
            WindowMin = f.WindowMin,
            WindowMax = f.WindowMax
        }));
    }

    [HttpGet("operators/range")]
    public async Task<ActionResult<IEnumerable<OperatorDailyFactDto>>> OperatorsRange([FromQuery] string from, [FromQuery] string to)
    {
        var dFrom = ParseDateOnly(from);
        var dTo = ParseDateOnly(to);
        if (dTo < dFrom) return BadRequest("'to' debe ser >= 'from'.");
        if (dFrom < UtcToday().AddDays(-7)) return BadRequest("Máx 7 días hacia atrás.");

        var facts = await _db.OperatorDailyFacts
            .Where(f => f.ServiceDate >= dFrom && f.ServiceDate <= dTo)
            .OrderBy(f => f.ServiceDate)
            .ToListAsync();

        var ids = facts.Select(f => f.OperatorUserId).Distinct().ToList();
        var map = await _db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Email ?? x.Id.ToString());

        return Ok(facts.Select(f => new OperatorDailyFactDto
        {
            ServiceDate = f.ServiceDate,
            OperatorUserId = f.OperatorUserId,
            OperatorName = map.TryGetValue(f.OperatorUserId, out var name) ? name : null,
            ServedCount = f.ServedCount,
            AvgServeToCompleteSec = f.AvgServeToCompleteSec,
            AvgTotalLeadTimeSec = f.AvgTotalLeadTimeSec,
            WindowMin = f.WindowMin,
            WindowMax = f.WindowMax
        }));
    }

    // ==== NUEVO: LISTA DETALLADA (HOY y RANGO) ===============================

    public sealed class TurnRowDto
    {
        public DateOnly ServiceDate { get; set; }
        public int Number { get; set; }
        public string Kind { get; set; } = default!;
        public string FinalStatus { get; set; } = default!;
        public int? WindowNumber { get; set; }
        public Guid? OperatorUserId { get; set; }
        public string? OperatorName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CalledAt { get; set; }
        public DateTime? ServedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? SkippedAt { get; set; }
        public int? WaitToCallSec { get; set; }
        public int? CallToServeSec { get; set; }
        public int? ServeToCompleteSec { get; set; }
        public int? TotalLeadTimeSec { get; set; }
    }

    private static IQueryable<TurnRowDto> ShapeRowsAsQueryable(IEnumerable<FactRow> src)
        => src.Select(r => new TurnRowDto
        {
            ServiceDate = r.ServiceDate,
            Number = r.Number,
            Kind = r.Kind,
            FinalStatus = r.FinalStatus,
            WindowNumber = r.WindowNumber,
            OperatorUserId = r.OperatorUserId,
            CreatedAt = r.CreatedAt,
            CalledAt = r.CalledAt,
            ServedAt = r.ServedAt,
            CompletedAt = r.CompletedAt,
            SkippedAt = r.SkippedAt,
            WaitToCallSec = r.WaitToCallSec,
            CallToServeSec = r.CallToServeSec,
            ServeToCompleteSec = r.ServeToCompleteSec,
            TotalLeadTimeSec = r.TotalLeadTimeSec
        }).AsQueryable();

    private async Task HydrateOperatorNames(IEnumerable<TurnRowDto> rows)
    {
        var ids = rows.Select(x => x.OperatorUserId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0) return;

        var map = await _db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Email ?? x.Id.ToString());

        foreach (var r in rows)
            if (r.OperatorUserId.HasValue && map.TryGetValue(r.OperatorUserId.Value, out var name))
                r.OperatorName = name;
    }

    /// <summary>
    /// Lista detallada de atenciones de HOY. Soporta filtros y ordenación.
    /// </summary>
    /// <param name="status">DONE|SKIPPED|PENDING|CALLED|SERVING (opcional)</param>
    /// <param name="kind">NORMAL|DISABILITY|SPECIAL (opcional)</param>
    /// <param name="windowNumber">filtra por ventanilla</param>
    /// <param name="operatorUserId">filtra por operador</param>
    /// <param name="onlyAttended">si true, solo DONE o SKIPPED</param>
    /// <param name="order">created|called|served|completed (default: completed desc)</param>
    /// <param name="page">0-based</param>
    /// <param name="pageSize">1..200</param>
    [HttpGet("today-list")]
    public async Task<ActionResult<object>> TodayList(
        [FromQuery] string? status = null,
        [FromQuery] string? kind = null,
        [FromQuery] int? windowNumber = null,
        [FromQuery] Guid? operatorUserId = null,
        [FromQuery] bool onlyAttended = false,
        [FromQuery] string order = "completed",
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 50)
    {
        var d = UtcToday();
        var rows = await GetRows(d, d);

        var q = ShapeRowsAsQueryable(rows);

        if (onlyAttended)
            q = q.Where(r => r.FinalStatus == "DONE" || r.FinalStatus == "SKIPPED");

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToUpperInvariant();
            q = q.Where(r => r.FinalStatus == s);
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var k = kind.Trim().ToUpperInvariant();
            q = q.Where(r => r.Kind == k);
        }

        if (windowNumber.HasValue)
            q = q.Where(r => r.WindowNumber == windowNumber);

        if (operatorUserId.HasValue)
            q = q.Where(r => r.OperatorUserId == operatorUserId);

        // Orden
        q = order switch
        {
            "created" => q.OrderByDescending(r => r.CreatedAt),
            "called" => q.OrderByDescending(r => r.CalledAt),
            "served" => q.OrderByDescending(r => r.ServedAt),
            _ => q.OrderByDescending(r => r.CompletedAt) // completed por defecto
        };

        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = q.Count();
        var items = q.Skip(page * pageSize).Take(pageSize).ToList();

        await HydrateOperatorNames(items);

        return Ok(new
        {
            date = d,
            total,
            page,
            pageSize,
            items
        });
    }

    /// <summary>
    /// Lista detallada en rango con filtros y paginación.
    /// </summary>
    [HttpGet("range-list")]
    public async Task<ActionResult<object>> RangeList(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] string? status = null,
        [FromQuery] string? kind = null,
        [FromQuery] int? windowNumber = null,
        [FromQuery] Guid? operatorUserId = null,
        [FromQuery] bool onlyAttended = false,
        [FromQuery] string order = "completed",
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 50)
    {
        var dFrom = ParseDateOnly(from);
        var dTo = ParseDateOnly(to);
        if (dTo < dFrom) return BadRequest("'to' debe ser >= 'from'.");

        var rows = await GetRows(dFrom, dTo);
        var q = ShapeRowsAsQueryable(rows);

        if (onlyAttended)
            q = q.Where(r => r.FinalStatus == "DONE" || r.FinalStatus == "SKIPPED");

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim().ToUpperInvariant();
            q = q.Where(r => r.FinalStatus == s);
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var k = kind.Trim().ToUpperInvariant();
            q = q.Where(r => r.Kind == k);
        }

        if (windowNumber.HasValue)
            q = q.Where(r => r.WindowNumber == windowNumber);

        if (operatorUserId.HasValue)
            q = q.Where(r => r.OperatorUserId == operatorUserId);

        q = order switch
        {
            "created" => q.OrderByDescending(r => r.CreatedAt),
            "called" => q.OrderByDescending(r => r.CalledAt),
            "served" => q.OrderByDescending(r => r.ServedAt),
            _ => q.OrderByDescending(r => r.CompletedAt)
        };

        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = q.Count();
        var items = q.Skip(page * pageSize).Take(pageSize).ToList();

        await HydrateOperatorNames(items);

        return Ok(new
        {
            from = dFrom,
            to = dTo,
            total,
            page,
            pageSize,
            items
        });
    }

    // ==== Auxiliar: join con Users (BASE EXISTENTE) ==========================

    private async Task HydrateOperatorNames(StatsResponseDto dto)
    {
        var ids = dto.ByOperator.Select(x => x.OperatorUserId).Distinct().ToList();
        if (ids.Count == 0) return;

        var map = await _db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Email ?? x.Id.ToString());

        foreach (var op in dto.ByOperator)
        {
            if (map.TryGetValue(op.OperatorUserId, out var name))
                op.OperatorName = name;
        }
    }
}
