// DTOs/Admin/CreateUserDto.cs
namespace Siestur.DTOs.Admin;
public class CreateUserDto
{
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    // "Admin" | "Colaborador"
    public string Role { get; set; } = "Colaborador";
}
