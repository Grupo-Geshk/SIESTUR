// DTOs/Auth/LoginRequestDto.cs
namespace Siestur.DTOs.Auth;

public class LoginRequestDto
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}
