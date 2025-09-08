// DTOs/Public/BoardWindowDto.cs
namespace Siestur.DTOs.Public;
public class BoardWindowDto
{
    public int WindowNumber { get; set; }
    public int? CurrentTurn { get; set; }   // turno llamado/atendiendo, si hay
    public string? Status { get; set; }     // CALLED | SERVING | null
}
