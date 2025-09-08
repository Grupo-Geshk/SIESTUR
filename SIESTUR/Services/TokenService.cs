// Services/TokenService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Siestur.Models;

namespace Siestur.Services;

public class TokenService : ITokenService
{
    public (string token, DateTime expiresAt) BuildToken(User user)
    {
        var key = Environment.GetEnvironmentVariable("Jwt__Key")
                  ?? throw new InvalidOperationException("Falta Jwt__Key");
        var issuer = Environment.GetEnvironmentVariable("Jwt__Issuer")
                    ?? throw new InvalidOperationException("Falta Jwt__Issuer");

        var expires = DateTime.UtcNow.AddHours(12);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role) 
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: null,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return (jwt, expires);
    }
}
