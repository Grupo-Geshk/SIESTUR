// DTOs/Turns/RecentTurnsResponseDto.cs
namespace Siestur.DTOs.Turns;

public class RecentTurnsResponseDto
{
    public IEnumerable<TurnResponseDto> Items { get; set; } = Enumerable.Empty<TurnResponseDto>();
    public int Count => Items.Count();
}
