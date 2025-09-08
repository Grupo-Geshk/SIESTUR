using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Siestur.Data;
using Siestur.DTOs.Windows;
using Siestur.Models;
using Siestur.Services.Hubs;

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

    // ====== INICIAR SESIÓN EN UNA VENTANILLA ======
    // Colaborador elige el número de ventanilla (creada por Admin) y abre su sesión de trabajo.
    // Requisito: el colaborador puede elegir qué número de ventanilla utilizará el día. :contentReference[oaicite:3]{index=3}
    [HttpPost("sessions")]
    public async Task<ActionResult> StartWindowSession([FromBody] StartWindowSessionDto dto)
    {
        if (dto.WindowNumber <= 0) return BadRequest("Número de ventanilla inválido.");

        var win = await _db.Windows.FirstOrDefaultAsync(w => w.Number == dto.WindowNumber && w.Active);
        if (win is null) return NotFound("La ventanilla no existe o está inactiva.");

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        // Cierra cualquier sesión previa abierta del usuario
        var openMine = await _db.WorkerSessions
            .Where(ws => ws.UserId == userId && ws.EndedAt == null)
            .ToListAsync();
        foreach (var s in openMine) s.EndedAt = DateTime.UtcNow;

        // (Regla simple) Permitimos sesiones concurrentes en la misma ventanilla si el otro ya cerró.
        // Si quieres bloqueo duro por ventanilla, valida que no existan otras sesiones abiertas con ese WindowId.

        var session = new WorkerSession
        {
            UserId = userId,
            Mode = "WINDOW",
            WindowId = win.Id,
            StartedAt = DateTime.UtcNow
        };
        _db.WorkerSessions.Add(session);
        await _db.SaveChangesAsync();

        await _windowsHub.Clients.All.SendAsync("windows:updated");
        return Ok(new { message = "Sesión de ventanilla iniciada.", windowNumber = win.Number });
    }

    // ====== TOMAR SIGUIENTE TURNO (FIFO) ======
    // Lógica: primer Turn con Status=PENDING ordenado por Number ASC, CreatedAt ASC. :contentReference[oaicite:4]{index=4}
    [HttpPost("{windowNumber:int}/next")]
    public async Task<ActionResult<WindowActionResponseDto>> TakeNext([FromRoute] int windowNumber)
    {
        var win = await _db.Windows.AsNoTracking().FirstOrDefaultAsync(w => w.Number == windowNumber && w.Active);
        if (win is null) return NotFound("Ventanilla no encontrada.");

        var turn = await _db.Turns
            .Where(t => t.Status == "PENDING")
            .OrderBy(t => t.Number).ThenBy(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (turn is null) return NotFound("No hay turnos pendientes.");

        turn.Status = "CALLED";
        turn.CalledAt = DateTime.UtcNow;
        turn.WindowId = win.Id;

        await _db.SaveChangesAsync();

        var resp = new WindowActionResponseDto
        {
            TurnId = turn.Id,
            TurnNumber = turn.Number,
            Status = turn.Status,
            WindowNumber = windowNumber,
            CalledAt = turn.CalledAt
        };

        // Reactividad: notificar a TV y vistas
        await _turnsHub.Clients.All.SendAsync("turns:updated", resp);
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return Ok(resp);
    }

    // ====== MARCAR "ATENDIENDO" (SERVING) ======
    [HttpPost("{windowNumber:int}/serve/{turnId:guid}")]
    public async Task<ActionResult<WindowActionResponseDto>> Serve([FromRoute] int windowNumber, [FromRoute] Guid turnId)
    {
        var win = await _db.Windows.AsNoTracking().FirstOrDefaultAsync(w => w.Number == windowNumber && w.Active);
        if (win is null) return NotFound("Ventanilla no encontrada.");

        var turn = await _db.Turns.FirstOrDefaultAsync(t => t.Id == turnId);
        if (turn is null) return NotFound("Turno no encontrado.");
        if (turn.WindowId == null) return BadRequest("El turno no está asignado a una ventanilla.");
        if (turn.Status != "CALLED") return BadRequest("El turno debe estar en estado CALLED para servir.");

        turn.Status = "SERVING";
        turn.ServedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var resp = new WindowActionResponseDto
        {
            TurnId = turn.Id,
            TurnNumber = turn.Number,
            Status = turn.Status,
            WindowNumber = windowNumber,
            CalledAt = turn.CalledAt,
            ServedAt = turn.ServedAt
        };

        await _turnsHub.Clients.All.SendAsync("turns:updated", resp);
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return Ok(resp);
    }

    // ====== COMPLETAR ======
    [HttpPost("{windowNumber:int}/complete/{turnId:guid}")]
    public async Task<ActionResult<WindowActionResponseDto>> Complete([FromRoute] int windowNumber, [FromRoute] Guid turnId)
    {
        var win = await _db.Windows.AsNoTracking().FirstOrDefaultAsync(w => w.Number == windowNumber && w.Active);
        if (win is null) return NotFound("Ventanilla no encontrada.");

        var turn = await _db.Turns.FirstOrDefaultAsync(t => t.Id == turnId);
        if (turn is null) return NotFound("Turno no encontrado.");
        if (turn.Status != "SERVING" && turn.Status != "CALLED")
            return BadRequest("Solo se puede completar un turno en CALLED o SERVING.");

        turn.Status = "DONE";
        await _db.SaveChangesAsync();

        var resp = new WindowActionResponseDto
        {
            TurnId = turn.Id,
            TurnNumber = turn.Number,
            Status = turn.Status,
            WindowNumber = windowNumber,
            CalledAt = turn.CalledAt,
            ServedAt = turn.ServedAt
        };

        await _turnsHub.Clients.All.SendAsync("turns:updated", resp);
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return Ok(resp);
    }

    // ====== OMITIR (SKIP) ======
    [HttpPost("{windowNumber:int}/skip/{turnId:guid}")]
    public async Task<ActionResult<WindowActionResponseDto>> Skip([FromRoute] int windowNumber, [FromRoute] Guid turnId)
    {
        var win = await _db.Windows.AsNoTracking().FirstOrDefaultAsync(w => w.Number == windowNumber && w.Active);
        if (win is null) return NotFound("Ventanilla no encontrada.");

        var turn = await _db.Turns.FirstOrDefaultAsync(t => t.Id == turnId);
        if (turn is null) return NotFound("Turno no encontrado.");
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
            SkippedAt = turn.SkippedAt
        };

        await _turnsHub.Clients.All.SendAsync("turns:updated", resp);
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return Ok(resp);
    }

    // ====== OVERVIEW (para TV/paneles) ======
    // Muestra ventanillas y su turno actual + próximos N turnos pendientes. Vista pública la servirá otro controller con key, pero esto entrega la data.
    // Requisito: la pantalla principal debe mostrar ventanillas, turno atendiendo y próximos. :contentReference[oaicite:5]{index=5}
    [HttpGet("overview")]
    [AllowAnonymous] // si la vas a usar para la TV pública, puedes dejarlo en otro controller con key
    public async Task<ActionResult<OverviewResponseDto>> Overview([FromQuery] int upcoming = 10)
    {
        upcoming = Math.Clamp(upcoming, 1, 50);

        var windows = await _db.Windows.AsNoTracking()
            .Where(w => w.Active)
            .OrderBy(w => w.Number)
            .ToListAsync();

        // Turnos "actuales" = último CALLED/SERVING por ventanilla
        var nowByWin = await _db.Turns.AsNoTracking()
            .Where(t => t.WindowId != null && (t.Status == "CALLED" || t.Status == "SERVING"))
            .GroupBy(t => t.WindowId)
            .Select(g => g.OrderByDescending(x => x.CalledAt).First())
            .ToListAsync();

        var mapWinIdToTurn = nowByWin.ToDictionary(x => x.WindowId!.Value, x => x);

        var winDtos = new List<WindowNowDto>();
        foreach (var w in windows)
        {
            if (mapWinIdToTurn.TryGetValue(w.Id, out var t))
                winDtos.Add(new WindowNowDto { WindowNumber = w.Number, CurrentTurn = t.Number, Status = t.Status });
            else
                winDtos.Add(new WindowNowDto { WindowNumber = w.Number, CurrentTurn = null, Status = null });
        }

        var upcomings = await _db.Turns.AsNoTracking()
            .Where(t => t.Status == "PENDING")
            .OrderBy(t => t.Number).ThenBy(t => t.CreatedAt)
            .Take(upcoming)
            .Select(t => t.Number)
            .ToListAsync();

        return Ok(new OverviewResponseDto
        {
            Windows = winDtos,
            Upcoming = upcomings
        });
    }
}
