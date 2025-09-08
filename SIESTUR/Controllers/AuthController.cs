// Controllers/AuthController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Siestur.Data;
using Siestur.DTOs.Auth;
using Siestur.Services;
using BCrypt.Net;
using Siestur.DTOs.Admin; // si reutilizas UserResponseDto, o usa AuthUserDto (ya definido)
using Siestur.DTOs.Auth;
using Siestur.Models;

namespace Siestur.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthController(ApplicationDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return Unauthorized("Credenciales inválidas.");

        var email = dto.Email.Trim().ToLower();
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.Active);

        if (user is null)
            return Unauthorized("Credenciales inválidas.");

        var ok = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!ok)
            return Unauthorized("Credenciales inválidas.");

        var (token, expiresAt) = _tokenService.BuildToken(user);

        return Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = new AuthUserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role
            }
        });
    }
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthUserDto>> Me()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.Active);
        if (user is null) return Unauthorized();

        return Ok(new AuthUserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role
        });
    }

    [HttpPost("admin-register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> AdminRegister([FromBody] AdminRegisterRequestDto dto)
    {
        // 1) Validar llave de registro
        var regKey = Environment.GetEnvironmentVariable("Admin__RegisterKey");
        if (string.IsNullOrWhiteSpace(regKey) ||
            !string.Equals(regKey, dto.RegisterKey?.Trim(), StringComparison.Ordinal))
            return Unauthorized("RegisterKey inválida.");

        // 2) Validaciones básicas
        if (string.IsNullOrWhiteSpace(dto.Name) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest("Faltan campos obligatorios (Name, Email, Password).");

        var email = dto.Email.Trim().ToLower();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == email);
        if (exists) return Conflict("Ya existe un usuario con ese correo.");

        // 3) Crear Admin
        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = email,
            Role = "Admin",
            Active = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // 4) Emitir JWT
        var (token, expiresAt) = _tokenService.BuildToken(user);

        return Ok(new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = new AuthUserDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role
            }
        });
    }
}
