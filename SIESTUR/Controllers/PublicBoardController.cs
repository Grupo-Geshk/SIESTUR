// Controllers/PublicBoardController.cs  (V3)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siestur.Data;
using SIESTUR.DTOs;

namespace Siestur.Controllers;

[ApiController]
[Route("public")]
public class PublicBoardController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public PublicBoardController(ApplicationDbContext db) => _db = db;

    // GET /public/board?key=xxxxx&upcoming=10
    [HttpGet("board")]
    [AllowAnonymous]
    public async Task<ActionResult<BoardResponseDto>> Board([FromQuery] string key, [FromQuery] int upcoming = 10)
    {
        if (string.IsNullOrWhiteSpace(key)) return Unauthorized("Missing key.");

        // === Validar llave ===
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var tvKeyDb = settings?.TvPublicKey;
        var tvKeyEnv = Environment.GetEnvironmentVariable("Siestur__TvPublicKey");
        var validKey = tvKeyDb ?? tvKeyEnv;
        if (string.IsNullOrWhiteSpace(validKey) || !string.Equals(validKey, key, StringComparison.Ordinal))
            return Unauthorized("Invalid key.");

        upcoming = Math.Clamp(upcoming, 1, 50);

        // === Ventanillas activas (ordenadas) ===
        var windows = await _db.Windows.AsNoTracking()
            .Where(w => w.Active)
            .OrderBy(w => w.Number)
            .ToListAsync();

        // Último turno CALLED/SERVING por ventanilla (turno visible en TV)
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
                // SPECIAL no se muestra en TV
                if (string.Equals(t.Kind, "SPECIAL", StringComparison.Ordinal))
                {
                    return new BoardWindowDto
                    {
                        WindowNumber = w.Number,
                        CurrentTurn = null,
                        Status = null,
                        Kind = null
                    };
                }

                return new BoardWindowDto
                {
                    WindowNumber = w.Number,
                    CurrentTurn = t.Number,
                    Status = t.Status,
                    Kind = t.Kind // NORMAL o DISABILITY
                };
            }

            return new BoardWindowDto
            {
                WindowNumber = w.Number,
                CurrentTurn = null,
                Status = null,
                Kind = null
            };
        }).ToList();

        // === Próximos DISABILITY (FIFO) ===
        var upDisObjs = await _db.Turns.AsNoTracking()
            .Where(t => t.Status == "PENDING" && t.Kind == "DISABILITY")
            .OrderBy(t => t.Number).ThenBy(t => t.CreatedAt)
            .Take(upcoming)
            .Select(t => new UpcomingItemDto { Number = t.Number, Kind = t.Kind })
            .ToListAsync();

        // === Próximos NORMAL (FIFO) — excluye SPECIAL
        var upNormObjsTyped = await _db.Turns.AsNoTracking()
            .Where(t => t.Status == "PENDING" && t.Kind == "NORMAL")
            .OrderBy(t => t.Number).ThenBy(t => t.CreatedAt)
            .Take(upcoming)
            .Select(t => new UpcomingItemDto { Number = t.Number, Kind = t.Kind })
            .ToListAsync();

        // Cola ordenada con tipo: ♿ primero, luego NORMAL. Tope global.
        var upcomingOrdered = upDisObjs.Concat(upNormObjsTyped)
            .Take(upcoming)
            .ToList();

        // Compatibilidad legacy: solo números
        var upDis = upDisObjs.Select(x => x.Number).ToList();
        var upNorm = upNormObjsTyped.Select(x => x.Number).ToList();
        var flatCompat = upcomingOrdered.Select(x => x.Number).ToList();

        // === Videos (orden por Position) ===
        var videos = await _db.Videos.AsNoTracking()
            .OrderBy(v => v.Position)
            .Select(v => new BoardVideoDto { Id = v.Id, Url = v.Url, Position = v.Position })
            .ToListAsync();

        // No-cache headers para TV
        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        return Ok(new BoardResponseDto
        {
            Windows = winDtos,
            Upcoming = flatCompat,              // (deprecado) solo números
            UpcomingDisability = upDis,         // opcional
            UpcomingNormal = upNorm,            // opcional
            UpcomingOrdered = upcomingOrdered,  // NUEVO: Number + Kind
            Videos = videos
        });
    }

    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok(new { ok = true, at = DateTime.UtcNow });
}
