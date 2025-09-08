// DTOs/Public/BoardResponseDto.cs
namespace Siestur.DTOs.Public;
public class BoardResponseDto
{
    public IEnumerable<BoardWindowDto> Windows { get; set; } = Enumerable.Empty<BoardWindowDto>();
    public IEnumerable<int> Upcoming { get; set; } = Enumerable.Empty<int>();
    public IEnumerable<BoardVideoDto> Videos { get; set; } = Enumerable.Empty<BoardVideoDto>();
}
