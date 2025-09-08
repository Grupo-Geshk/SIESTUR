// DTOs/Turns/TurnResponseDto.cs
namespace Siestur.DTOs.Turns;

public class TurnResponseDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Status { get; set; } = default!;
    public int? WindowNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? ServedAt { get; set; }
    public DateTime? SkippedAt { get; set; }
}
