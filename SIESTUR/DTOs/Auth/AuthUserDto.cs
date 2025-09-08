// DTOs/Auth/AuthUserDto.cs
namespace Siestur.DTOs.Auth;

public class AuthUserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
}
