namespace SIESTUR.DTOs
{
    public class UpcomingItemDto
    {
        public int Number { get; set; }
        public string Kind { get; set; } = "NORMAL"; // NORMAL | DISABILITY | SPECIAL
    }

    public class BoardResponseDto
    {
        public IEnumerable<BoardWindowDto> Windows { get; set; } = Enumerable.Empty<BoardWindowDto>();
        public IEnumerable<int> Upcoming { get; set; } = Enumerable.Empty<int>(); // LEGACY (se puede dejar)
        public IEnumerable<BoardVideoDto> Videos { get; set; } = Enumerable.Empty<BoardVideoDto>();

        // V2 existente
        public IEnumerable<int> UpcomingNormal { get; set; } = Enumerable.Empty<int>();
        public IEnumerable<int> UpcomingDisability { get; set; } = Enumerable.Empty<int>();

        // NUEVO: lista combinada en orden real con tipo (lo que usará el TV)
        public IEnumerable<UpcomingItemDto> UpcomingOrdered { get; set; } = Enumerable.Empty<UpcomingItemDto>();
    }

    public class BoardVideoDto
    {
        public Guid Id { get; set; }
        public string Url { get; set; } = default!;
        public int Position { get; set; }
    }

    public class BoardWindowDto
    {
        public int WindowNumber { get; set; }
        public int? CurrentTurn { get; set; }          // turno llamado/atendiendo, si hay
        public string? Status { get; set; }            // CALLED | SERVING | null
        public string? Kind { get; set; }              // NORMAL | DISABILITY | SPECIAL (V2)
        public bool IsDisability => string.Equals(Kind, "DISABILITY", StringComparison.OrdinalIgnoreCase);
    }
}
