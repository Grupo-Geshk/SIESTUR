// Models/Dto/StatsDtos.cs
using System.ComponentModel.DataAnnotations;

namespace Siestur.Models.Dto;

// === Resumen alto nivel para tarjetas/overview ===
public class StatsSummaryDto
{
    public int TotalTurns { get; set; }
    public int DoneCount { get; set; }
    public int SkippedCount { get; set; }
    public int DisabilityCount { get; set; }
    public int SpecialCount { get; set; }

    // Promedios en segundos (front los formatea)
    public double? AvgWaitToCallSec { get; set; }       // Created -> Called
    public double? AvgCallToServeSec { get; set; }      // Called  -> Served
    public double? AvgServeToCompleteSec { get; set; }  // Served  -> Completed
    public double? AvgTotalLeadTimeSec { get; set; }    // Created -> Completed (o mejor proxy)
}

// === Ranking por operador ===
public class OperatorStatsDto
{
    public Guid OperatorUserId { get; set; }
    public string? OperatorName { get; set; }
    public int ServedCount { get; set; }
    public double? AvgServeToCompleteSec { get; set; }
    public double? AvgTotalLeadTimeSec { get; set; }
}

// === Ranking por ventanilla ===
public class WindowStatsDto
{
    public int WindowNumber { get; set; }
    public int ServedCount { get; set; }
    public double? AvgServeToCompleteSec { get; set; }
    public double? AvgTotalLeadTimeSec { get; set; }
}

// === Serie diaria (hasta 7 días) ===
public class DailyStatsPointDto
{
    [Required] public DateOnly Date { get; set; }
    public int TotalTurns { get; set; }
    public int DoneCount { get; set; }
    public int SkippedCount { get; set; }

    public double? AvgWaitToCallSec { get; set; }
    public double? AvgCallToServeSec { get; set; }
    public double? AvgServeToCompleteSec { get; set; }
    public double? AvgTotalLeadTimeSec { get; set; }

    // NUEVO: facilita resaltar el día actual en UI
    public bool IsToday { get; set; }
}

// === Fila detallada para listas de admin (hoy/rango) ===
public class TurnRowDto
{
    public DateOnly ServiceDate { get; set; }
    public int Number { get; set; }
    public string Kind { get; set; } = default!;
    public string FinalStatus { get; set; } = default!;
    public int? WindowNumber { get; set; }
    public Guid? OperatorUserId { get; set; }
    public string? OperatorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SkippedAt { get; set; }
    public int? WaitToCallSec { get; set; }
    public int? CallToServeSec { get; set; }
    public int? ServeToCompleteSec { get; set; }
    public int? TotalLeadTimeSec { get; set; }
}

// === Payload completo del endpoint (KPIs) ===
public class StatsResponseDto
{
    public StatsSummaryDto Summary { get; set; } = new();
    public List<OperatorStatsDto> ByOperator { get; set; } = new();
    public List<WindowStatsDto> ByWindow { get; set; } = new();
    public List<DailyStatsPointDto> Series { get; set; } = new();
}
