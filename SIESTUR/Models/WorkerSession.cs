// Models/WorkerSession.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Siestur.Models;

public class WorkerSession
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [Required, ForeignKey(nameof(User))] public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    [Required, MaxLength(10)] public string Mode { get; set; } = WorkerSessionMode.Assigner;
    [ForeignKey(nameof(Window))] public Guid? WindowId { get; set; }
    public Window? Window { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}
