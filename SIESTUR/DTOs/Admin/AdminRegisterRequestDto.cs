namespace Siestur.DTOs.Auth;

public class AdminRegisterRequestDto
{
    public string RegisterKey { get; set; } = default!; // debe coincidir con Admin__RegisterKey
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}
