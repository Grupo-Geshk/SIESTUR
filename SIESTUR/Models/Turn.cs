// Models/Turn.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Siestur.Models;

public class Turn
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public int Number { get; set; }
    [Required, MaxLength(10)] public string Status { get; set; } = TurnStatus.Pending;
    [MaxLength(20)] public string? Kind { get; set; } // NORMAL, SPECIAL, etc.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CalledAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SkippedAt { get; set; }

    [ForeignKey(nameof(Window))] public Guid? WindowId { get; set; }
    public Window? Window { get; set; }

    // User tracking for audit
    public Guid? CalledByUserId { get; set; }
    public Guid? ServedByUserId { get; set; }
    public Guid? CompletedByUserId { get; set; }
}
