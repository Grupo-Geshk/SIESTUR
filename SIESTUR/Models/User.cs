// Models/User.cs
using System.ComponentModel.DataAnnotations;

namespace Siestur.Models;

public class User
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(100)] public string Name { get; set; } = default!;
    [Required, MaxLength(100)] public string Email { get; set; } = default!;
    [Required] public string PasswordHash { get; set; } = default!;
    // Solo Admin o Colaborador
    [Required, MaxLength(20)] public string Role { get; set; } = "Colaborador";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
