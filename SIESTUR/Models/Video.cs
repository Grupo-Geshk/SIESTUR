// Models/Video.cs
using System.ComponentModel.DataAnnotations;

namespace Siestur.Models;

public class Video
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(300)] public string Url { get; set; } = default!;
    public int Position { get; set; }
}
