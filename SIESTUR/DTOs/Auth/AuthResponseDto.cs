// DTOs/Auth/AuthResponseDto.cs
namespace Siestur.DTOs.Auth;

public class AuthResponseDto
{
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public AuthUserDto User { get; set; } = default!;
}
