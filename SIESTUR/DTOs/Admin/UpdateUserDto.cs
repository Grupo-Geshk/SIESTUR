// DTOs/Admin/UpdateUserDto.cs
namespace Siestur.DTOs.Admin;
public class UpdateUserDto
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool? Active { get; set; }
    public string? NewPassword { get; set; }
}
