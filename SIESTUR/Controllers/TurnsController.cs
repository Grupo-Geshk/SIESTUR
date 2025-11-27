using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Siestur.Data;
using Siestur.Models;
using Siestur.Services;
using Siestur.Services.Hubs;
using SIESTUR.DTOs;

namespace Siestur.Controllers;

[ApiController]
[Route("turns")]
[Authorize] // Colaborador y Admin pueden crear/consultar
public class TurnsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<TurnsHub> _turnsHub;
    private readonly IDateTimeProvider _dateTime;

    public TurnsController(
        ApplicationDbContext db,
        IHubContext<TurnsHub> turnsHub,
        IDateTimeProvider dateTime)
    {
        _db = db;
        _turnsHub = turnsHub;
        _dateTime = dateTime;
    }

    /// <summary>
    /// Crea el siguiente turno PENDING, incrementando el contador del día.
    /// Opcionalmente permite fijar un número de inicio si StartOverride es mayor al contador actual.
    /// FIXED: Added Serializable isolation level to prevent race conditions
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TurnResponseDto>> Create([FromBody] CreateTurnRequestDto dto)
    {
        var today = _dateTime.Today;

        // FIXED: Use Serializable isolation level to prevent concurrent turn number assignment
        await using var tx = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        try
        {
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
                dc.NextNumber = start; // "iniciar desde" si el admin no reseteó pero el asignador lo necesita
            }

            var number = dc.NextNumber;
            dc.NextNumber++;

            var turn = new Turn
            {
                Number = number,
                Status = TurnStatus.Pending, // FIXED: Use constant instead of magic string
                CreatedAt = _dateTime.UtcNow // FIXED: Use injected IDateTimeProvider for testability
            };

            _db.Turns.Add(turn);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // FIXED: Removed N+1 query - no window at creation time
            var dtoResp = new TurnResponseDto
            {
                Id = turn.Id,
                Number = turn.Number,
                Status = turn.Status,
                WindowNumber = null,
                CreatedAt = turn.CreatedAt,
                CalledAt = turn.CalledAt,
                ServedAt = turn.ServedAt,
                SkippedAt = turn.SkippedAt
            };

            // Notificar a toda la app que hay un nuevo turno (reactivo)
            await _turnsHub.Clients.All.SendAsync("turns:created", dtoResp);

            return CreatedAtAction(nameof(GetRecent), new { limit = 1 }, dtoResp);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Devuelve los últimos N turnos asignados (por defecto 10).
    /// FIXED: Added Include to prevent N+1 query
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<RecentTurnsResponseDto>> GetRecent([FromQuery] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);
        var today = _dateTime.Today;

        var items = await _db.Turns
            .AsNoTracking()
            .Include(t => t.Window) // FIXED: Eager load to prevent N+1
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
    /// Lista simple de pendientes en FIFO (para debug o para vista del asignador).
    /// FIXED: Added Include to prevent N+1 query and used constant
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<TurnResponseDto>>> GetPending()
    {
        var today = _dateTime.Today;

        var items = await _db.Turns
            .AsNoTracking()
            .Include(t => t.Window) // FIXED: Eager load to prevent N+1
            .Where(t => t.Status == TurnStatus.Pending && // FIXED: Use constant
                       DateOnly.FromDateTime(t.CreatedAt) == today)
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
}
