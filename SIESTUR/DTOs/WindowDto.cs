namespace SIESTUR.DTOs
{
    public class OverviewResponseDto
    {
        public IEnumerable<WindowNowDto> Windows { get; set; } = Enumerable.Empty<WindowNowDto>();

        // V1 (compatibilidad)
        public IEnumerable<int> Upcoming { get; set; } = Enumerable.Empty<int>();

        // V2
        public IEnumerable<int> UpcomingNormal { get; set; } = Enumerable.Empty<int>();
        public IEnumerable<int> UpcomingDisability { get; set; } = Enumerable.Empty<int>();
    }

    public class WindowNowDto
    {
        public int WindowNumber { get; set; }
        public int? CurrentTurn { get; set; }
        public string? Status { get; set; }    // CALLED | SERVING | null
        public string? Kind { get; set; }      // NORMAL | DISABILITY | SPECIAL

        public bool IsDisability =>
            string.Equals(Kind, "DISABILITY", StringComparison.OrdinalIgnoreCase);
    }

    public class StartWindowSessionDto
    {
        public int WindowNumber { get; set; }
    }

    public class WindowActionResponseDto
    {
        public Guid TurnId { get; set; }
        public int TurnNumber { get; set; }
        public string Status { get; set; } = default!;
        public int WindowNumber { get; set; }

        public DateTime? CalledAt { get; set; }
        public DateTime? ServedAt { get; set; }
        public DateTime? SkippedAt { get; set; }
        public DateTime? CompletedAt { get; set; }   // NUEVO
        public string? Kind { get; set; }            // NORMAL | DISABILITY | SPECIAL
    }
}
