using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Siestur.Data;
using Siestur.DTOs.Admin;
using Siestur.Models;
using Siestur.Services.Hubs;

namespace Siestur.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<TurnsHub> _turnsHub;
    private readonly IHubContext<WindowsHub> _windowsHub;
    private readonly IHubContext<VideosHub> _videosHub;

    public AdminController(
        ApplicationDbContext db,
        IHubContext<TurnsHub> turnsHub,
        IHubContext<WindowsHub> windowsHub,
        IHubContext<VideosHub> videosHub)
    {
        _db = db;
        _turnsHub = turnsHub;
        _windowsHub = windowsHub;
        _videosHub = videosHub;
    }

    // ====== USERS ======

    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers()
    {
        var users = await _db.Users.AsNoTracking()
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
        if (string.IsNullOrWhiteSpace(dto.Name) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Faltan datos obligatorios.");

        var email = dto.Email.Trim().ToLower();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (exists) return Conflict("Ya existe un usuario con ese correo.");

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = string.IsNullOrWhiteSpace(dto.Role) ? "Colaborador" : dto.Role.Trim(),
            Active = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUsers), new { id = user.Id }, new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Active = user.Active,
            CreatedAt = user.CreatedAt
        });
    }

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<UserResponseDto>> UpdateUser([FromRoute] Guid id, [FromBody] UpdateUserDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Name)) user.Name = dto.Name.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var email = dto.Email.Trim().ToLower();
            var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email && u.Id != id);
            if (exists) return Conflict("Ya existe otro usuario con ese correo.");
            user.Email = email;
        }
        if (!string.IsNullOrWhiteSpace(dto.Role)) user.Role = dto.Role.Trim();
        if (dto.Active.HasValue) user.Active = dto.Active.Value;
        if (!string.IsNullOrWhiteSpace(dto.NewPassword)) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        await _db.SaveChangesAsync();

        return Ok(new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            Active = user.Active,
            CreatedAt = user.CreatedAt
        });
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ====== WINDOWS ======

    [HttpGet("windows")]
    public async Task<ActionResult<IEnumerable<WindowResponseDto>>> GetWindows()
    {
        var list = await _db.Windows.AsNoTracking()
            .OrderBy(w => w.Number)
            .Select(w => new WindowResponseDto { Id = w.Id, Number = w.Number, Active = w.Active })
            .ToListAsync();
        return Ok(list);
    }

    [HttpPost("windows")]
    public async Task<ActionResult<WindowResponseDto>> CreateWindow([FromBody] CreateWindowDto dto)
    {
        if (dto.Number <= 0) return BadRequest("El número de ventanilla debe ser > 0.");

        var exists = await _db.Windows.AnyAsync(w => w.Number == dto.Number);
        if (exists) return Conflict("Ya existe una ventanilla con ese número.");

        var win = new Window { Number = dto.Number, Active = true };
        _db.Windows.Add(win);
        await _db.SaveChangesAsync();

        // Notificar cambios a paneles
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return CreatedAtAction(nameof(GetWindows), new { id = win.Id },
            new WindowResponseDto { Id = win.Id, Number = win.Number, Active = win.Active });
    }

    [HttpPut("windows/{id:guid}")]
    public async Task<ActionResult<WindowResponseDto>> UpdateWindow([FromRoute] Guid id, [FromBody] UpdateWindowDto dto)
    {
        var win = await _db.Windows.FirstOrDefaultAsync(w => w.Id == id);
        if (win is null) return NotFound();

        if (dto.Number.HasValue)
        {
            if (dto.Number <= 0) return BadRequest("El número de ventanilla debe ser > 0.");
            var exists = await _db.Windows.AnyAsync(w => w.Number == dto.Number && w.Id != id);
            if (exists) return Conflict("Ya existe otra ventanilla con ese número.");
            win.Number = dto.Number.Value;
        }
        if (dto.Active.HasValue) win.Active = dto.Active.Value;

        await _db.SaveChangesAsync();
        await _windowsHub.Clients.All.SendAsync("windows:updated");

        return Ok(new WindowResponseDto { Id = win.Id, Number = win.Number, Active = win.Active });
    }

    [HttpDelete("windows/{id:guid}")]
    public async Task<IActionResult> DeleteWindow([FromRoute] Guid id)
    {
        var win = await _db.Windows.FirstOrDefaultAsync(w => w.Id == id);
        if (win is null) return NotFound();

        _db.Windows.Remove(win);
        await _db.SaveChangesAsync();
        await _windowsHub.Clients.All.SendAsync("windows:updated");
        return NoContent();
    }

    // ====== VIDEOS (cola de TV) ======

    [HttpGet("videos")]
    public async Task<ActionResult<IEnumerable<VideoResponseDto>>> GetVideos()
    {
        var vids = await _db.Videos.AsNoTracking()
            .OrderBy(v => v.Position)
            .Select(v => new VideoResponseDto { Id = v.Id, Url = v.Url, Position = v.Position })
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
            position = Math.Max(0, dto.Position.Value);
            // desplazar elementos que tengan posición >= position
            var toShift = await _db.Videos.Where(v => v.Position >= position).ToListAsync();
            foreach (var v in toShift) v.Position++;
        }
        else
        {
            position = (await _db.Videos.MaxAsync(v => (int?)v.Position)) ?? -1;
            position += 1;
        }

        var video = new Video { Url = dto.Url.Trim(), Position = position };
        _db.Videos.Add(video);
        await _db.SaveChangesAsync();

        await _videosHub.Clients.All.SendAsync("videos:updated");

        return CreatedAtAction(nameof(GetVideos), new { id = video.Id },
            new VideoResponseDto { Id = video.Id, Url = video.Url, Position = video.Position });
    }

    [HttpPut("videos/{id:guid}")]
    public async Task<ActionResult<VideoResponseDto>> UpdateVideo([FromRoute] Guid id, [FromBody] UpdateVideoDto dto)
    {
        var v = await _db.Videos.FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Url)) v.Url = dto.Url.Trim();

        if (dto.Position.HasValue && dto.Position.Value != v.Position)
        {
            var newPos = Math.Max(0, dto.Position.Value);
            // reordenar: liberar antigua y colocar nueva
            if (newPos > v.Position)
            {
                var toShiftDown = await _db.Videos
                    .Where(x => x.Position > v.Position && x.Position <= newPos && x.Id != v.Id)
                    .ToListAsync();
                foreach (var x in toShiftDown) x.Position--;
            }
            else
            {
                var toShiftUp = await _db.Videos
                    .Where(x => x.Position >= newPos && x.Position < v.Position && x.Id != v.Id)
                    .ToListAsync();
                foreach (var x in toShiftUp) x.Position++;
            }
            v.Position = newPos;
        }

        await _db.SaveChangesAsync();
        await _videosHub.Clients.All.SendAsync("videos:updated");

        return Ok(new VideoResponseDto { Id = v.Id, Url = v.Url, Position = v.Position });
    }

    [HttpDelete("videos/{id:guid}")]
    public async Task<IActionResult> DeleteVideo([FromRoute] Guid id)
    {
        var v = await _db.Videos.FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return NotFound();

        var oldPos = v.Position;
        _db.Videos.Remove(v);

        // compactar posiciones
        var toShift = await _db.Videos.Where(x => x.Position > oldPos).ToListAsync();
        foreach (var x in toShift) x.Position--;

        await _db.SaveChangesAsync();
        await _videosHub.Clients.All.SendAsync("videos:updated");

        return NoContent();
    }

    // ====== RESET DIARIO ======
    // Requerimiento: iniciar en 00 y que las ventanillas no tengan asignaciones del día anterior.
    // Además, confirmación destructiva con texto exacto.
    // (Se recomienda también cortar WorkerSessions abiertas y reiniciar el contador del día)
    [HttpPost("reset-day")]
    public async Task<IActionResult> ResetDay([FromBody] ResetDayRequestDto dto)
    {
        if (dto?.Confirmation?.Trim() != "Estoy seguro de borrar los turnos.")
            return BadRequest("Debe escribir exactamente: Estoy seguro de borrar los turnos.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Reiniciar contador del día
        var settings = await _db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var startDefault = settings?.StartNumberDefault ??
            int.Parse(Environment.GetEnvironmentVariable("Siestur__StartNumberDefault") ?? "0");

        var dc = await _db.DayCounters.FirstOrDefaultAsync(x => x.ServiceDate == today);
        if (dc is null)
        {
            _db.DayCounters.Add(new DayCounter { ServiceDate = today, NextNumber = startDefault });
        }
        else
        {
            dc.NextNumber = startDefault;
        }

        // Cerrar sesiones activas
        var openSessions = await _db.WorkerSessions.Where(ws => ws.EndedAt == null).ToListAsync();
        foreach (var s in openSessions) s.EndedAt = DateTime.UtcNow;

        // Limpiar turnos del día actual (PENDING, CALLED, SERVING) y soltar asociaciones
        var toDelete = await _db.Turns
            .Where(t => DateOnly.FromDateTime(t.CreatedAt) == today)
            .ToListAsync();

        _db.Turns.RemoveRange(toDelete);

        await _db.SaveChangesAsync();

        // Notificar a paneles (TV/ventanillas/asignador)
        await _turnsHub.Clients.All.SendAsync("turns:reset");
        await _windowsHub.Clients.All.SendAsync("windows:updated");
        await _videosHub.Clients.All.SendAsync("videos:updated"); // opcional, por si cambias la cola también

        return Ok(new { message = "Día reiniciado.", startDefault });
    }
}
