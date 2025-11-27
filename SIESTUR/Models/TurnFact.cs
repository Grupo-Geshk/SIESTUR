// Models/TurnFact.cs (NEW)
using System.ComponentModel.DataAnnotations;

namespace Siestur.Models;

public class TurnFact
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    // Día lógico del servicio (para agrupar por fecha)
    [Required] public DateOnly ServiceDate { get; set; }

    // Datos de identificación
    public int Number { get; set; }
    [MaxLength(20)] public string Kind { get; set; } = "NORMAL";     // NORMAL | DISABILITY | SPECIAL
    [MaxLength(10)] public string FinalStatus { get; set; } = "DONE"; // DONE|SKIPPED|...

    // Ventanilla/Operador (para rankings)
    public int? WindowNumber { get; set; }
    public Guid? OperatorUserId { get; set; }

    // Timestamps originales
    public DateTime CreatedAt { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SkippedAt { get; set; }

    // Duraciones precomputadas (segundos) = reportes rápidos
    public int? WaitToCallSec { get; set; }       // CalledAt - CreatedAt
    public int? CallToServeSec { get; set; }      // ServedAt - CalledAt
    public int? ServeToCompleteSec { get; set; }  // CompletedAt - ServedAt
    public int? TotalLeadTimeSec { get; set; }    // CompletedAt - CreatedAt (o lo disponible)
}
