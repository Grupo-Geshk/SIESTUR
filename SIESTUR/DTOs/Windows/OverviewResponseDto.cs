// DTOs/Windows/OverviewResponseDto.cs
namespace Siestur.DTOs.Windows;

public class OverviewResponseDto
{
    public IEnumerable<WindowNowDto> Windows { get; set; } = Enumerable.Empty<WindowNowDto>();
    public IEnumerable<int> Upcoming { get; set; } = Enumerable.Empty<int>(); // próximos N PENDING
}

public class WindowNowDto
{
    public int WindowNumber { get; set; }
    public int? CurrentTurn { get; set; }          // número de turno atendiendo/llamado
    public string? Status { get; set; }            // CALLED/SERVING o null si libre
}
