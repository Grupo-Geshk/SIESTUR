namespace SIESTUR.DTOs
{
    public class AdminRegisterRequestDto
    {
        public string RegisterKey { get; set; } = default!; // debe coincidir con Admin__RegisterKey
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
    }

    public class CreateUserDto
    {
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
        // "Admin" | "Colaborador"
        public string Role { get; set; } = "Colaborador";
    }

    public class CreateVideoDto
    {
        public string Url { get; set; } = default!;
        // opcional, si no se manda, se pone al final de la cola
        public int? Position { get; set; }
    }

    public class CreateWindowDto
    {
        public int Number { get; set; }
    }

    public class ResetDayRequestDto
    {
        // Debe ser EXACTAMENTE: "Estoy seguro de eliminar." (según requerimiento)
        public string Confirmation { get; set; } = default!;
    }

    public class WindowResponseDto
    {
        public Guid Id { get; set; }
        public int Number { get; set; }
        public bool Active { get; set; }
    }

    public class UpdateWindowDto
    {
        public int? Number { get; set; }
        public bool? Active { get; set; }
    }

    public class UserResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Role { get; set; } = default!;
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateVideoDto
    {
        public string? Url { get; set; }
        public int? Position { get; set; }
    }

    public class UpdateUserDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool? Active { get; set; }
        public string? NewPassword { get; set; }
    }

    public class VideoResponseDto
    {
        public Guid Id { get; set; }
        public string Url { get; set; }
        public int Position { get; set; }
    }
}
