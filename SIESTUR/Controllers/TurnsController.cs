using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Siestur.Data;
using Siestur.DTOs.Turns;
using Siestur.Models;
using Siestur.Services.Hubs;

namespace Siestur.Controllers;

[ApiController]
[Route("turns")]
[Authorize] // Colaborador y Admin pueden crear/consultar
public class TurnsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<TurnsHub> _turnsHub;

    public TurnsController(ApplicationDbContext db, IHubContext<TurnsHub> turnsHub)
    {
        _db = db;
        _turnsHub = turnsHub;
    }

    /// <summary>
    /// Crea el siguiente turno PENDING, incrementando el contador del día.
    /// Opcionalmente permite fijar un número de inicio si StartOverride es mayor al contador actual.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TurnResponseDto>> Create([FromBody] CreateTurnRequestDto dto)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Usamos una transacción simple para evitar condiciones de carrera en NextNumber.
        await using var tx = await _db.Database.BeginTransactionAsync();

        var dc = await _db.DayCounters.FirstOrDefaultAsync(x => x.ServiceDate == today);
        if (dc is null)
        {
            // start default: Settings o ENV
            var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
            var startDefault = settings?.StartNumberDefault
                ?? int.Parse(Environment.GetEnvironmentVariable("Siestur__StartNumberDefault") ?? "0");
            dc = new DayCounter { ServiceDate = today, NextNumber = startDefault };
            _db.DayCounters.Add(dc);
            await _db.SaveChangesAsync();
        }

        if (dto?.StartOverride is int start && start > dc.NextNumber)
        {
            dc.NextNumber = start; // “iniciar desde” si el admin no reseteó pero el asignador lo necesita
            await _db.SaveChangesAsync();
        }

        var number = dc.NextNumber;
        dc.NextNumber = number + 1;
        await _db.SaveChangesAsync();

        var turn = new Turn
        {
            Number = number,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _db.Turns.Add(turn);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var dtoResp = await MapToDto(turn);

        // Notificar a toda la app que hay un nuevo turno (reactivo)
        await _turnsHub.Clients.All.SendAsync("turns:created", dtoResp);

        return CreatedAtAction(nameof(GetRecent), new { limit = 1 }, dtoResp);
    }

    /// <summary>
    /// Devuelve los últimos N turnos asignados (por defecto 10).
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<RecentTurnsResponseDto>> GetRecent([FromQuery] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var items = await _db.Turns
            .AsNoTracking()
            .Where(t => DateOnly.FromDateTime(t.CreatedAt) == today)
            .OrderByDescending(t => t.Number) // últimos asignados primero
            .Take(limit)
            .Select(t => new TurnResponseDto
            {
                Id = t.Id,
                Number = t.Number,
                Status = t.Status,
                WindowNumber = t.Window != null ? t.Window.Number : (int?)null,
                CreatedAt = t.CreatedAt,
                CalledAt = t.CalledAt,
                ServedAt = t.ServedAt,
                SkippedAt = t.SkippedAt
            })
            .ToListAsync();

        return Ok(new RecentTurnsResponseDto { Items = items });
    }

    /// <summary>
    /// (Opcional) Lista simple de pendientes en FIFO (para debug o para vista del asignador).
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<TurnResponseDto>>> GetPending()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = await _db.Turns
            .AsNoTracking()
            .Where(t => t.Status == "PENDING" && DateOnly.FromDateTime(t.CreatedAt) == today)
            .OrderBy(t => t.Number) // FIFO por número ascendente
            .ThenBy(t => t.CreatedAt)
            .Select(t => new TurnResponseDto
            {
                Id = t.Id,
                Number = t.Number,
                Status = t.Status,
                WindowNumber = t.Window != null ? t.Window.Number : (int?)null,
                CreatedAt = t.CreatedAt,
                CalledAt = t.CalledAt,
                ServedAt = t.ServedAt,
                SkippedAt = t.SkippedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    // ---- helpers ----
    private async Task<TurnResponseDto> MapToDto(Turn t)
    {
        int? windowNumber = null;
        if (t.WindowId.HasValue)
        {
            var w = await _db.Windows.AsNoTracking().FirstOrDefaultAsync(x => x.Id == t.WindowId.Value);
            windowNumber = w?.Number;
        }

        return new TurnResponseDto
        {
            Id = t.Id,
            Number = t.Number,
            Status = t.Status,
            WindowNumber = windowNumber,
            CreatedAt = t.CreatedAt,
            CalledAt = t.CalledAt,
            ServedAt = t.ServedAt,
            SkippedAt = t.SkippedAt
        };
    }
}
