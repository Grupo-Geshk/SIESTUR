// DTOs/Turns/CreateTurnRequestDto.cs
namespace Siestur.DTOs.Turns;

public class CreateTurnRequestDto
{
    // Opcional: si el asignador quiere iniciar desde otro número (UI)
    public int? StartOverride { get; set; }
}
