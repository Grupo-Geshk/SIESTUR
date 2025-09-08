// Models/Window.cs
using System.ComponentModel.DataAnnotations;

namespace Siestur.Models;

public class Window
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public int Number { get; set; }
    public bool Active { get; set; } = true;
}
