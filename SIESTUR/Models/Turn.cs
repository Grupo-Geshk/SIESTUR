// Models/Turn.cs (v2.0)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Siestur.Models;

public class Turn
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public int Number { get; set; }

    // PENDING|CALLED|SERVING|DONE|SKIPPED
    [Required, MaxLength(10)] public string Status { get; set; } = "PENDING";

    // NEW: tipo de turno -> NORMAL | DISABILITY | SPECIAL
    [Required, MaxLength(20)] public string Kind { get; set; } = "NORMAL";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CalledAt { get; set; }
    public DateTime? ServedAt { get; set; }

    // NEW: para cerrar métricas
    public DateTime? CompletedAt { get; set; }
    public DateTime? SkippedAt { get; set; }

    // Relación con ventanilla
    [ForeignKey(nameof(Window))] public Guid? WindowId { get; set; }
    public Window? Window { get; set; }

    // NEW: trazabilidad de operador (quien cambió de estado)
    public Guid? CalledByUserId { get; set; }
    public Guid? ServedByUserId { get; set; }
    public Guid? CompletedByUserId { get; set; }
}
