// Models/Dto/OperatorDailyFactDto.cs
namespace Siestur.Models.Dto;

public class OperatorDailyFactDto
{
    public DateOnly ServiceDate { get; set; }
    public Guid OperatorUserId { get; set; }
    public string? OperatorName { get; set; }
    public int ServedCount { get; set; }
    public double? AvgServeToCompleteSec { get; set; }
    public double? AvgTotalLeadTimeSec { get; set; }
    public int? WindowMin { get; set; }
    public int? WindowMax { get; set; }
}
