// DTOs/Windows/WindowActionResponseDto.cs
namespace Siestur.DTOs.Windows;
public class WindowActionResponseDto
{
    public Guid TurnId { get; set; }
    public int TurnNumber { get; set; }
    public string Status { get; set; } = default!; // CALLED | SERVING | DONE | SKIPPED
    public int WindowNumber { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public DateTime? SkippedAt { get; set; }
}
