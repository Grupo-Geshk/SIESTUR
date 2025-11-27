// Models/OperatorDailyFact.cs
using System.ComponentModel.DataAnnotations;

namespace Siestur.Models;

public class OperatorDailyFact
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public DateOnly ServiceDate { get; set; }
    [Required] public Guid OperatorUserId { get; set; }

    public int ServedCount { get; set; }
    public double? AvgServeToCompleteSec { get; set; }
    public double? AvgTotalLeadTimeSec { get; set; }
    public int? WindowMin { get; set; }
    public int? WindowMax { get; set; }
}
