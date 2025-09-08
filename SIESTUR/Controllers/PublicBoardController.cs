using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siestur.Data;
using Siestur.DTOs.Public;

namespace Siestur.Controllers;

[ApiController]
[Route("public")]
public class PublicBoardController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public PublicBoardController(ApplicationDbContext db) => _db = db;

    // GET /public/board?key=xxxxx
    // Vista pública para TV: muestra ventanillas, turno actual y próximos; incluye cola de videos.
    // Según requisitos: “Ver Turnos” desde el login, sin autenticación. :contentReference[oaicite:2]{index=2}
    [HttpGet("board")]
    [AllowAnonymous]
    public async Task<ActionResult<BoardResponseDto>> Board([FromQuery] string key, [FromQuery] int upcoming = 10)
    {
        if (string.IsNullOrWhiteSpace(key)) return Unauthorized("Missing key.");

        // Validar llave pública desde Settings o, fallback, desde ENV
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var tvKeyDb = settings?.TvPublicKey;
        var tvKeyEnv = Environment.GetEnvironmentVariable("Siestur__TvPublicKey");
        var validKey = tvKeyDb ?? tvKeyEnv;

        if (string.IsNullOrWhiteSpace(validKey) || !string.Equals(validKey, key, StringComparison.Ordinal))
            return Unauthorized("Invalid key.");

        upcoming = Math.Clamp(upcoming, 1, 50);

        // Ventanillas activas
        var windows = await _db.Windows.AsNoTracking()
            .Where(w => w.Active)
            .OrderBy(w => w.Number)
            .ToListAsync();

        // Por ventana: último turn CALLED/SERVING (turno actual visible en TV) :contentReference[oaicite:3]{index=3}
        var nowByWin = await _db.Turns.AsNoTracking()
            .Where(t => t.WindowId != null && (t.Status == "CALLED" || t.Status == "SERVING"))
            .GroupBy(t => t.WindowId)
            .Select(g => g.OrderByDescending(x => x.CalledAt).First())
            .ToListAsync();

        var mapWinIdToTurn = nowByWin.ToDictionary(x => x.WindowId!.Value, x => x);

        var winDtos = windows.Select(w =>
        {
            if (mapWinIdToTurn.TryGetValue(w.Id, out var t))
                return new BoardWindowDto { WindowNumber = w.Number, CurrentTurn = t.Number, Status = t.Status };
            return new BoardWindowDto { WindowNumber = w.Number, CurrentTurn = null, Status = null };
        }).ToList();

        // Próximos turnos (PENDING, FIFO por Number ASC, CreatedAt ASC) :contentReference[oaicite:4]{index=4}
        var upcomings = await _db.Turns.AsNoTracking()
            .Where(t => t.Status == "PENDING")
            .OrderBy(t => t.Number).ThenBy(t => t.CreatedAt)
            .Take(upcoming)
            .Select(t => t.Number)
            .ToListAsync();

        // Cola de videos (orden por Position) gestionada por el Admin :contentReference[oaicite:5]{index=5}
        var videos = await _db.Videos.AsNoTracking()
            .OrderBy(v => v.Position)
            .Select(v => new BoardVideoDto { Id = v.Id, Url = v.Url, Position = v.Position })
            .ToListAsync();

        // Cache headers amables para TV (se actualiza vía SignalR en otras vistas, pero esto ayuda si recarga)
        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        return Ok(new BoardResponseDto
        {
            Windows = winDtos,
            Upcoming = upcomings,
            Videos = videos
        });
    }

    // (Opcional) Ping/health público para el TV player
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok(new { ok = true, at = DateTime.UtcNow });
}
