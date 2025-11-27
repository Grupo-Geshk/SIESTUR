// Controllers/WindowsController.cs  (V3 - bloqueo de ventanillas + ownership de sesión)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Siestur.Data;
using Siestur.Models;
using Siestur.Services.Hubs;
using SIESTUR.DTOs;
using System.Security.Claims;

namespace Siestur.Controllers;

[ApiController]
[Route("windows")]
[Authorize] // Admin y Colaborador
public class WindowsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<WindowsHub> _windowsHub;
    private readonly IHubContext<TurnsHub> _turnsHub;

    public WindowsController(
        ApplicationDbContext db,
        IHubContext<WindowsHub> windowsHub,
        IHubContext<TurnsHub> turnsHub)
    {
        _db = db;
        _windowsHub = windowsHub;
        _turnsHub = turnsHub;
    }

    // =============== Helpers ===============
    private Guid? GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdStr, out var userId) ? userId : null;
    }

    private async Task<WorkerSession?> GetMyOpenSessionAsync(Guid userId) =>
        await _db.WorkerSessions
            .Include(ws => ws.Window)
            .Where(ws => ws.UserId == userId && ws.EndedAt == null)
            .OrderByDescending(ws => ws.StartedAt)
            .FirstOrDefaultAsync();

    private async Task<bool> HasOwnershipAsync(Guid userId, int windowNumber)
    {
        var mine = await GetMyOpenSessionAsync(userId);
        return mine?.Window?.Number == windowNumber;
    }

    // =============== INICIAR SESIÓN EN UNA VENTANILLA ===============
    [HttpPost("sessions")]
    public async Task<ActionResult> StartWindowSession([FromBody] StartWindowSessionDto dto)
    {
        if (dto.WindowNumber <= 0) return BadRequest("Número de ventanilla inválido.");

        var win = await _db.Windows.FirstOrDefaultAsync(w => w.Number == dto.WindowNumber && w.Active);
        if (win is null) return NotFound("La ventanilla no existe o está inactiva.");

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Cierra cualquier sesión previa abierta del usuario
        var openMine = await _db.WorkerSessions
            .Where(ws => ws.UserId == userId && ws.EndedAt == null)
            .ToListAsync();
        foreach (var s in openMine) s.EndedAt = DateTime.UtcNow;

        // Verifica si la ventanilla ya está tomada por otro colaborador
        var taken = await _db.WorkerSessions.AnyAsync(ws => ws.WindowId == win.Id && ws.EndedAt == null);
        if (taken) return Conflict($"La ventanilla {win.Number} ya está ocupada.");

        var session = new WorkerSession
        {
            UserId = userId.Value,
            Mode = "WINDOW",
            WindowId = win.Id,
            StartedAt = DateTime.UtcNow
        };
        _db.WorkerSessions.Add(session);
        await _db.SaveChangesAsync();

        await _windowsHub.Clients.All.SendAsync("windows:updated");
        return Ok(new { message = "Sesión de ventanilla iniciada.", windowNumber = win.Number });
    }

    // =============== CERRAR MI SESIÓN DE VENTANILLA ===============
    [HttpDelete("sessions")]
    public async Task<ActionResult> EndMyWindowSession()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var mine = await GetMyOpenSessionAsync(userId.Value);
        if (mine is null) return NotFound("No tienes una sesión de ventanilla activa.");

        mine.EndedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _windowsHub.Clients.All.SendAsync("windows:updated");
        return Ok(new { message = "Sesión de ventanilla cerrada.", windowNumber = mine.Window?.Number });
    }

    // =============== VER MI SESIÓN ACTIVA (opcional para front) ===============
    [HttpGet("sessions/me")]
    public async Task<ActionResult> GetMySession()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var mine = await GetMyOpenSessionAsync(userId.Value);
        if (mine is null) return Ok(new { active = false });

        return Ok(new
        {
            active = true,
            windowNumber = mine.Window?.Number,
            startedAt = mine.StartedAt
        });
    }

    // =============== TOMAR SIGUIENTE TURNO ===============
    // Soporta ?kind=NORMAL|DISABILITY|SPECIAL (opcional). Si no se pasa, prioriza DISABILITY y luego FIFO.
    // Requiere sesión activa del colaborador en esta ventanilla.
    [HttpPost("{windowNumber:int}/next")]
    public async Task<ActionResult<WindowActionResponseDto>> TakeNext(
        [FromRoute] int windowNumber,
        [FromQuery] string? kind = null)
    {
        var win = await _db.Windows.AsNoTracking().FirstOrDefaultAsync(w => w.Number == windowNumber && w.Active);
        if (win is null) return NotFound("Ventanilla no encontrada.");

        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await HasOwnershipAsync(userId.Value, windowNumber))
            return Forbid("No tienes una sesión activa en esta ventanilla.");

        IQueryable<Turn> q = _db.Turns.Where(t => t.Status == "PENDING");

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var k = kind.Trim().ToUpperInvariant();
            if (k is not ("NORMAL" or "DISABILITY" or "SPECIAL"))
                return BadRequest("Parámetro 'kind' inválido. Use NORMAL, DISABILITY o SPECIAL.");
            q = q.Where(t => t.Kind == k);
        }

        var turn = await q
            .OrderBy(t => t.Kind == "DISABILITY" ? 0 : 1) // prioridad ♿
            .ThenBy(t => t.Number)
            .ThenBy(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (turn is null) return NotFound("No hay turnos pendientes" + (kind is null ? "." : $" del tipo {kind}."));

        turn.Status = "CALLED";
        turn.CalledAt = DateTime.UtcNow;
        turn.CalledByUserId = userId.Value;
        turn.WindowId = win.Id;

        await _db.SaveChangesAsync();

        var resp = new WindowActionResponseDto
        {
            TurnId = turn.Id,
            TurnNumber = turn.Number,
            Status = turn.Status,
            WindowNumber = windowNumber,
            CalledAt = turn.CalledAt,
            Kind = turn.Kind
        };

        await _turnsHub.Clients.All.SendAsync("turns:updated", resp);
        await _windowsHub.Clients.All.SendAsync("windows:updated");
        await _windowsHub.Clients.All.SendAsync("windows:bell", new { windowNumber, turnNumber = turn.Number });
        return Ok(resp);
    }

    // =============== MARCAR "SERVING" ===============
    // Requiere sesión activa del colaborador en esta ventanilla.
    [HttpPost("{windowNumber:int}/serve/{turnId:guid}")]
    public async Task<ActionResult<WindowActionResponseDto>> Serve([FromRoute] int windowNumber, [FromRoute] Guid turnId)
    {
        var win = await _db.Windows.AsNoTracking().FirstOrDefaultAsync(w => w.Number == windowNumber && w.Active);
        if (win is null) return NotFound("Ventanilla no encontrada.");

        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await HasOwnershipAsync(userId.Value, windowNumber))
            return Forbid("No tienes una sesión activa en esta ventanilla.");

        var turn = await _db.Turns.FirstOrDefaultAsync(t => t.Id == turnId);
        if (turn is null) return NotFound("Turno no encontrado.");
        if (turn.WindowId == null) return BadRequest("El turno no está asignado a una ventanilla.");
        if (turn.WindowId != win.Id) return BadRequest("El turno pertenece a otra ventanilla.");
        if (turn.Status != "CALLED") return BadRequest("El turno debe estar en estado CALLED para servir.");

        turn.Status = "SERVING";
        turn.ServedAt = DateTime.UtcNow;
        turn.ServedByUserId = userId.Value;
        await _db.SaveChangesAsync();

        var resp = new WindowActionResponseDto
        {
            TurnId = turn.Id,
            TurnNumber = turn.Number,
            Status = turn.Status,
            WindowNumber = windowNumber,
            CalledAt = turn.CalledAt,
            ServedAt = turn.ServedAt,
            Kind = turn.Kind
        };

        await _turnsHub.Clients.All.SendAsync("turns:updated", resp);
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return Ok(resp);
    }

    // =============== COMPLETAR ===============
    // Requiere sesión activa del colaborador en esta ventanilla.
    [HttpPost("{windowNumber:int}/complete/{turnId:guid}")]
    public async Task<ActionResult<WindowActionResponseDto>> Complete([FromRoute] int windowNumber, [FromRoute] Guid turnId)
    {
        var win = await _db.Windows.AsNoTracking().FirstOrDefaultAsync(w => w.Number == windowNumber && w.Active);
        if (win is null) return NotFound("Ventanilla no encontrada.");

        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await HasOwnershipAsync(userId.Value, windowNumber))
            return Forbid("No tienes una sesión activa en esta ventanilla.");

        var turn = await _db.Turns.FirstOrDefaultAsync(t => t.Id == turnId);
        if (turn is null) return NotFound("Turno no encontrado.");
        if (turn.WindowId == null) return BadRequest("El turno no está asignado a una ventanilla.");
        if (turn.WindowId != win.Id) return BadRequest("El turno pertenece a otra ventanilla.");
        if (turn.Status != "SERVING" && turn.Status != "CALLED")
            return BadRequest("Solo se puede completar un turno en CALLED o SERVING.");

        turn.Status = "DONE";
        turn.CompletedAt = DateTime.UtcNow;
        turn.CompletedByUserId = userId.Value;
        await _db.SaveChangesAsync();

        var resp = new WindowActionResponseDto
        {
            TurnId = turn.Id,
            TurnNumber = turn.Number,
            Status = turn.Status,
            WindowNumber = windowNumber,
            CalledAt = turn.CalledAt,
            ServedAt = turn.ServedAt,
            CompletedAt = turn.CompletedAt,
            Kind = turn.Kind
        };

        await _turnsHub.Clients.All.SendAsync("turns:updated", resp);
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return Ok(resp);
    }

    // =============== OMITIR (SKIP) ===============
    // Requiere sesión activa del colaborador en esta ventanilla.
    [HttpPost("{windowNumber:int}/skip/{turnId:guid}")]
    public async Task<ActionResult<WindowActionResponseDto>> Skip([FromRoute] int windowNumber, [FromRoute] Guid turnId)
    {
        var win = await _db.Windows.AsNoTracking().FirstOrDefaultAsync(w => w.Number == windowNumber && w.Active);
        if (win is null) return NotFound("Ventanilla no encontrada.");

        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await HasOwnershipAsync(userId.Value, windowNumber))
            return Forbid("No tienes una sesión activa en esta ventanilla.");

        var turn = await _db.Turns.FirstOrDefaultAsync(t => t.Id == turnId);
        if (turn is null) return NotFound("Turno no encontrado.");
        if (turn.WindowId == null) return BadRequest("El turno no está asignado a una ventanilla.");
        if (turn.WindowId != win.Id) return BadRequest("El turno pertenece a otra ventanilla.");
        if (turn.Status != "CALLED")
            return BadRequest("Solo se puede omitir un turno en estado CALLED.");

        turn.Status = "SKIPPED";
        turn.SkippedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var resp = new WindowActionResponseDto
        {
            TurnId = turn.Id,
            TurnNumber = turn.Number,
            Status = turn.Status,
            WindowNumber = windowNumber,
            CalledAt = turn.CalledAt,
            ServedAt = turn.ServedAt,
            SkippedAt = turn.SkippedAt,
            Kind = turn.Kind
        };

        await _turnsHub.Clients.All.SendAsync("turns:updated", resp);
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return Ok(resp);
    }

    // =============== CAMPANA ===============
    // Requiere sesión activa del colaborador en esta ventanilla.
    [HttpPost("{windowNumber:int}/bell")]
    public async Task<ActionResult> RingBell([FromRoute] int windowNumber)
    {
        var win = await _db.Windows.AsNoTracking().FirstOrDefaultAsync(w => w.Number == windowNumber && w.Active);
        if (win is null) return NotFound("Ventanilla no encontrada.");

        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        if (!await HasOwnershipAsync(userId.Value, windowNumber))
            return Forbid("No tienes una sesión activa en esta ventanilla.");

        await _windowsHub.Clients.All.SendAsync("windows:bell", new { windowNumber });

        return Ok(new { message = "Campana enviada." });
    }

    // =============== OVERVIEW (para paneles internos) ===============
    // Nota: aquí mantenemos SPECIAL visible (la TV se maneja en PublicBoardController).
    [HttpGet("overview")]
    [AllowAnonymous]
    public async Task<ActionResult<OverviewResponseDto>> Overview([FromQuery] int upcoming = 10)
    {
        upcoming = Math.Clamp(upcoming, 1, 50);

        // Ventanillas activas
        var windows = await _db.Windows.AsNoTracking()
            .Where(w => w.Active)
            .OrderBy(w => w.Number)
            .ToListAsync();

        // Turno actual (CALLED/SERVING) por ventanilla
        var nowByWin = await _db.Turns.AsNoTracking()
            .Where(t => t.WindowId != null && (t.Status == "CALLED" || t.Status == "SERVING"))
            .GroupBy(t => t.WindowId)
            .Select(g => g.OrderByDescending(x => x.CalledAt).First())
            .ToListAsync();

        var mapWinIdToTurn = nowByWin.ToDictionary(x => x.WindowId!.Value, x => x);

        var winDtos = windows.Select(w =>
        {
            if (mapWinIdToTurn.TryGetValue(w.Id, out var t))
            {
                return new WindowNowDto
                {
                    WindowNumber = w.Number,
                    CurrentTurn = t.Number,
                    Status = t.Status,
                    Kind = t.Kind
                };
            }
            return new WindowNowDto { WindowNumber = w.Number };
        }).ToList();

        // Próximos ♿
        var upDis = await _db.Turns.AsNoTracking()
            .Where(t => t.Status == "PENDING" && t.Kind == "DISABILITY")
            .OrderBy(t => t.Number).ThenBy(t => t.CreatedAt)
            .Take(upcoming)
            .Select(t => t.Number)
            .ToListAsync();

        // Próximos normales/especiales (para panel interno sí mostramos SPECIAL)
        var upNorm = await _db.Turns.AsNoTracking()
            .Where(t => t.Status == "PENDING" && t.Kind != "DISABILITY")
            .OrderBy(t => t.Number).ThenBy(t => t.CreatedAt)
            .Take(upcoming)
            .Select(t => t.Number)
            .ToListAsync();

        var flatCompat = upDis.Concat(upNorm).Take(upcoming).ToList();

        return Ok(new OverviewResponseDto
        {
            Windows = winDtos,
            Upcoming = flatCompat,
            UpcomingDisability = upDis,
            UpcomingNormal = upNorm
        });
    }
}
