// DTOs/Admin/CreateVideoDto.cs
namespace Siestur.DTOs.Admin;
public class CreateVideoDto
{
    public string Url { get; set; } = default!;
    // opcional, si no se manda, se pone al final de la cola
    public int? Position { get; set; }
}
