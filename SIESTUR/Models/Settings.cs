using System.ComponentModel.DataAnnotations;

namespace Siestur.Models;

public class Settings
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public int StartNumberDefault { get; set; } = 0;
    [MaxLength(200)] public string TvPublicKey { get; set; } = default!;

}

public class SystemState
{
    [Key] public int Id { get; set; } = 1;
    public DateOnly? LastDailyReset { get; set; }
}