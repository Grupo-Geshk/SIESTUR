namespace SIESTUR.DTOs;

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

    // NUEVO
    public DateTime? CompletedAt { get; set; }
    public string Kind { get; set; } = "NORMAL";
}

public class RecentTurnsResponseDto
{
    public IEnumerable<TurnResponseDto> Items { get; set; } = Enumerable.Empty<TurnResponseDto>();
    public int Count => Items.Count();
}

public class CreateTurnRequestDto
{
    // Opcional: si el asignador quiere iniciar desde otro número (UI)
    public int? StartOverride { get; set; }

    // NUEVO: tipo de turno NORMAL | DISABILITY | SPECIAL
    public string Kind { get; set; } = "NORMAL";
}
