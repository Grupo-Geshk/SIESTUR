// DTOs/Admin/UserResponseDto.cs
namespace Siestur.DTOs.Admin;
public class UserResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
}
