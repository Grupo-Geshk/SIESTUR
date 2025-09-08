// DTOs/Public/BoardVideoDto.cs
namespace Siestur.DTOs.Public;
public class BoardVideoDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = default!;
    public int Position { get; set; }
}
