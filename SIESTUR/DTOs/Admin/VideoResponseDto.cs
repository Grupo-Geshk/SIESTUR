// DTOs/Admin/VideoResponseDto.cs
namespace Siestur.DTOs.Admin;
public class VideoResponseDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = default!;
    public int Position { get; set; }
}
