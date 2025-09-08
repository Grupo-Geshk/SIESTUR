// DTOs/Admin/ResetDayRequestDto.cs
namespace Siestur.DTOs.Admin;
public class ResetDayRequestDto
{
    // Debe ser EXACTAMENTE: "Estoy seguro de eliminar." (según requerimiento)
    public string Confirmation { get; set; } = default!;
}
