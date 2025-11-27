// Controllers/AdminController.cs  (V3 alineado)
// - Reset-day: confirmación "Estoy seguro de eliminar.", snapshot del día, reinicio de numeración (fallback 1), cierre de sesiones y notificaciones SignalR.
// - Purge-now: cierra sesiones, elimina TODOS los turnos, reinicia numeración (fallback 1) y notifica.
// - CRUD de Usuarios, Ventanillas y Videos tal como pediste.
// Nota: Usa DTOs de SIESTUR.DTOs y StatsDtos ya alineados.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Siestur.Data;
using Siestur.Models;
using Siestur.Services.Hubs;
using SIESTUR.DTOs;

namespace Siestur.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<TurnsHub> _turnsHub;
    private readonly IHubContext<WindowsHub> _windowsHub;

    public AdminController(
        ApplicationDbContext db,
        IHubContext<TurnsHub> turnsHub,
        IHubContext<WindowsHub> windowsHub)
    {
        _db = db;
        _turnsHub = turnsHub;
        _windowsHub = windowsHub;
    }

    private static DateOnly UtcToday() => DateOnly.FromDateTime(DateTime.UtcNow);

    // ------------------------------------------------------------------------
    // USUARIOS
    // ------------------------------------------------------------------------

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> ListUsers()
    {
        var users = await _db.Users.AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role,
                Active = u.Active,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<ActionResult<UserResponseDto>> CreateUser([FromBody] CreateUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Email y Password son requeridos.");

        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(x => x.Email == email))
            return Conflict("Ya existe un usuario con ese email.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = dto.Name?.Trim() ?? string.Empty,
            Email = email,
            Role = string.IsNullOrWhiteSpace(dto.Role) ? UserRole.Colaborador : dto.Role.Trim(),
            Active = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(ListUsers), new { id = user.Id }, new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name!,
            Email = user.Email!,
            Role = user.Role!,
            Active = user.Active,
            CreatedAt = user.CreatedAt
        });
    }

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<UserResponseDto>> UpdateUser([FromRoute] Guid id, [FromBody] UpdateUserDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var exists = await _db.Users.AnyAsync(x => x.Email == email && x.Id != id);
            if (exists) return Conflict("Ya existe otro usuario con ese email.");
            user.Email = email;
        }

        if (!string.IsNullOrWhiteSpace(dto.Name))
            user.Name = dto.Name.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Role))
            user.Role = dto.Role.Trim();

        if (dto.Active.HasValue)
            user.Active = dto.Active.Value;

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        await _db.SaveChangesAsync();

        return Ok(new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name!,
            Email = user.Email!,
            Role = user.Role!,
            Active = user.Active,
            CreatedAt = user.CreatedAt
        });
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null) return NotFound();

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ------------------------------------------------------------------------
    // VENTANILLAS
    // ------------------------------------------------------------------------

    [HttpGet("windows")]
    public async Task<ActionResult<IEnumerable<WindowResponseDto>>> ListWindows()
    {
        var list = await _db.Windows.AsNoTracking()
            .OrderBy(w => w.Number)
            .Select(w => new WindowResponseDto
            {
                Id = w.Id,
                Number = w.Number,
                Active = w.Active
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost("windows")]
    public async Task<ActionResult<WindowResponseDto>> CreateWindow([FromBody] CreateWindowDto dto)
    {
        if (dto.Number <= 0) return BadRequest("Número inválido.");
        if (await _db.Windows.AnyAsync(x => x.Number == dto.Number))
            return Conflict("Ya existe una ventanilla con ese número.");

        var w = new Window
        {
            Id = Guid.NewGuid(),
            Number = dto.Number,
            Active = true
        };
        _db.Windows.Add(w);
        await _db.SaveChangesAsync();

        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return CreatedAtAction(nameof(ListWindows), new { id = w.Id }, new WindowResponseDto
        {
            Id = w.Id,
            Number = w.Number,
            Active = w.Active
        });
    }

    [HttpPut("windows/{id:guid}")]
    public async Task<ActionResult<WindowResponseDto>> UpdateWindow([FromRoute] Guid id, [FromBody] UpdateWindowDto dto)
    {
        var w = await _db.Windows.FirstOrDefaultAsync(x => x.Id == id);
        if (w is null) return NotFound();

        if (dto.Number.HasValue)
        {
            if (dto.Number.Value <= 0) return BadRequest("Número inválido.");
            var exists = await _db.Windows.AnyAsync(x => x.Number == dto.Number && x.Id != id);
            if (exists) return Conflict("Ya existe otra ventanilla con ese número.");
            w.Number = dto.Number.Value;
        }

        if (dto.Active.HasValue)
            w.Active = dto.Active.Value;

        await _db.SaveChangesAsync();
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return Ok(new WindowResponseDto
        {
            Id = w.Id,
            Number = w.Number,
            Active = w.Active
        });
    }

    [HttpDelete("windows/{id:guid}")]
    public async Task<IActionResult> DeleteWindow([FromRoute] Guid id)
    {
        var w = await _db.Windows.FirstOrDefaultAsync(x => x.Id == id);
        if (w is null) return NotFound();

        _db.Windows.Remove(w);
        await _db.SaveChangesAsync();
        await _windowsHub.Clients.All.SendAsync("windows:updated");
        return NoContent();
    }

    // ------------------------------------------------------------------------
    // VIDEOS
    // ------------------------------------------------------------------------

    [HttpGet("videos")]
    public async Task<ActionResult<IEnumerable<VideoResponseDto>>> ListVideos()
    {
        var vids = await _db.Videos.AsNoTracking()
            .OrderBy(v => v.Position)
            .Select(v => new VideoResponseDto
            {
                Id = v.Id,
                Url = v.Url,
                Position = v.Position
            })
            .ToListAsync();

        return Ok(vids);
    }

    [HttpPost("videos")]
    public async Task<ActionResult<VideoResponseDto>> CreateVideo([FromBody] CreateVideoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Url)) return BadRequest("Url requerida.");

        int position;
        if (dto.Position.HasValue)
        {
            position = dto.Position.Value;
        }
        else
        {
            var max = await _db.Videos.MaxAsync(v => (int?)v.Position) ?? 0;
            position = max + 1;
        }

        var vid = new Video
        {
            Id = Guid.NewGuid(),
            Url = dto.Url.Trim(),
            Position = position
        };
        _db.Videos.Add(vid);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(ListVideos), new { id = vid.Id }, new VideoResponseDto
        {
            Id = vid.Id,
            Url = vid.Url,
            Position = vid.Position
        });
    }

    [HttpPut("videos/{id:guid}")]
    public async Task<ActionResult<VideoResponseDto>> UpdateVideo([FromRoute] Guid id, [FromBody] UpdateVideoDto dto)
    {
        var v = await _db.Videos.FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Url))
            v.Url = dto.Url.Trim();

        if (dto.Position.HasValue)
            v.Position = dto.Position.Value;

        await _db.SaveChangesAsync();

        return Ok(new VideoResponseDto
        {
            Id = v.Id,
            Url = v.Url,
            Position = v.Position
        });
    }

    [HttpDelete("videos/{id:guid}")]
    public async Task<IActionResult> DeleteVideo([FromRoute] Guid id)
    {
        var v = await _db.Videos.FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return NotFound();

        _db.Videos.Remove(v);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ------------------------------------------------------------------------
    // RESET DEL DÍA (con snapshot + reinicio numeración)
    // ------------------------------------------------------------------------

    [HttpPost("reset-day")]
    public async Task<IActionResult> ResetDay([FromBody] ResetDayRequestDto dto)
    {
        const string Expected = "Estoy seguro de eliminar."; // unificado con DTO
        if (!string.Equals(dto?.Confirmation?.Trim(), Expected, StringComparison.Ordinal))
            return BadRequest($"Debe escribir exactamente: {Expected}");

        var today = UtcToday();

        // 1) Cerrar sesiones abiertas
        var openSessions = await _db.WorkerSessions.Where(ws => ws.EndedAt == null).ToListAsync();
        foreach (var s in openSessions) s.EndedAt = DateTime.UtcNow;

        // 2) Snapshot de turnos del día a TurnFacts
        var turnsToday = await _db.Turns
            .Include(t => t.Window)
            .Where(t => DateOnly.FromDateTime(t.CreatedAt.ToUniversalTime()) == today)
            .ToListAsync();

        var facts = new List<TurnFact>();
        foreach (var t in turnsToday)
        {
            int? s(DateTime? a, DateTime? b) =>
                (a.HasValue && b.HasValue) ? (int?)(a.Value - b.Value).TotalSeconds : null;

            var totalLead = t.CompletedAt ?? t.ServedAt ?? t.CalledAt;

            facts.Add(new TurnFact
            {
                Id = Guid.NewGuid(),
                ServiceDate = today,
                Number = t.Number,
                Kind = t.Kind ?? "NORMAL",
                FinalStatus = (t.Status ?? "PENDING").ToUpperInvariant(),
                WindowNumber = t.Window?.Number,
                OperatorUserId = t.CompletedByUserId ?? t.ServedByUserId ?? t.CalledByUserId,
                CreatedAt = t.CreatedAt,
                CalledAt = t.CalledAt,
                ServedAt = t.ServedAt,
                CompletedAt = t.CompletedAt,
                SkippedAt = t.SkippedAt,
                WaitToCallSec = s(t.CalledAt, t.CreatedAt),
                CallToServeSec = s(t.ServedAt, t.CalledAt),
                ServeToCompleteSec = s(t.CompletedAt, t.ServedAt),
                TotalLeadTimeSec = s(totalLead, t.CreatedAt)
            });
        }

        if (facts.Count > 0)
        {
            await _db.TurnFacts.AddRangeAsync(facts);
        }

        // 3) OperatorDailyFacts (del día)
        if (facts.Count > 0)
        {
            var byOperator = facts
                .Where(x => x.OperatorUserId.HasValue)
                .GroupBy(x => x.OperatorUserId!.Value)
                .Select(g => new OperatorDailyFact
                {
                    Id = Guid.NewGuid(),
                    ServiceDate = today,
                    OperatorUserId = g.Key,
                    ServedCount = g.Count(x => x.ServedAt.HasValue || x.FinalStatus == "DONE"),
                    AvgServeToCompleteSec = Avg(g.Select(x => x.ServeToCompleteSec)),
                    AvgTotalLeadTimeSec = Avg(g.Select(x => x.TotalLeadTimeSec)),
                    WindowMin = g.Min(x => x.WindowNumber),
                    WindowMax = g.Max(x => x.WindowNumber)
                }).ToList();

            if (byOperator.Count > 0)
                await _db.OperatorDailyFacts.AddRangeAsync(byOperator);
        }

        // 4) Eliminar TODOS los turnos (limpieza general como solicitaste)
        _db.Turns.RemoveRange(_db.Turns);

        // 5) Reiniciar contador del día a startDefault (fallback ENV/1)
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var startDefault = settings?.StartNumberDefault
            ?? int.Parse(Environment.GetEnvironmentVariable("Siestur__StartNumberDefault") ?? "1");

        var dc = await _db.DayCounters.FirstOrDefaultAsync(x => x.ServiceDate == today);
        if (dc is null)
            _db.DayCounters.Add(new DayCounter { ServiceDate = today, NextNumber = startDefault });
        else
            dc.NextNumber = startDefault;

        await _db.SaveChangesAsync();

        // 6) Notificar
        await _turnsHub.Clients.All.SendAsync("turns:reset");
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return Ok(new
        {
            message = "Día reiniciado. Turnos del día archivados en estadísticas y numeración reiniciada.",
            startDefault,
            turnsArchived = facts.Count
        });
    }

    // ------------------------------------------------------------------------
    // PURGA AGRESIVA (sin snapshot)
    // ------------------------------------------------------------------------

    [HttpPost("turns/purge-now")]
    public async Task<IActionResult> PurgeNow()
    {
        // 1) Cerrar sesiones
        var open = await _db.WorkerSessions.Where(ws => ws.EndedAt == null).ToListAsync();
        foreach (var s in open) s.EndedAt = DateTime.UtcNow;

        // 2) Eliminar TODOS los turnos
        _db.Turns.RemoveRange(_db.Turns);

        // 3) Reiniciar contador del día a startDefault (consistente con reset-day)
        var today = UtcToday();
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var startDefault = settings?.StartNumberDefault
            ?? int.Parse(Environment.GetEnvironmentVariable("Siestur__StartNumberDefault") ?? "1");

        var dc = await _db.DayCounters.FirstOrDefaultAsync(x => x.ServiceDate == today);
        if (dc is null)
            _db.DayCounters.Add(new DayCounter { ServiceDate = today, NextNumber = startDefault });
        else
            dc.NextNumber = startDefault;

        await _db.SaveChangesAsync();

        // 4) Notificar
        await _turnsHub.Clients.All.SendAsync("turns:reset");
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return NoContent();
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private static double? Avg(IEnumerable<int?> xs)
    {
        var vals = xs.Where(x => x.HasValue).Select(x => (double)x!.Value).ToList();
        return vals.Count == 0 ? null : vals.Average();
    }
}
