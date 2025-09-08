// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using Siestur.Models;

namespace Siestur.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<Window> Windows { get; set; }
    public DbSet<DayCounter> DayCounters { get; set; }
    public DbSet<Turn> Turns { get; set; }
    public DbSet<WorkerSession> WorkerSessions { get; set; }
    public DbSet<Video> Videos { get; set; }
    public DbSet<Settings> Settings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Window.Number único
        modelBuilder.Entity<Window>()
            .HasIndex(w => w.Number)
            .IsUnique();

        // WorkerSession -> Window (no cascada; no queremos borrar sesiones si se borra la ventanilla)
        modelBuilder.Entity<WorkerSession>()
            .HasOne(ws => ws.Window)
            .WithMany()
            .HasForeignKey(ws => ws.WindowId)
            .OnDelete(DeleteBehavior.Restrict);

        // Turn -> Window (si se borra la ventanilla, dejar null)
        modelBuilder.Entity<Turn>()
            .HasOne(t => t.Window)
            .WithMany()
            .HasForeignKey(t => t.WindowId)
            .OnDelete(DeleteBehavior.SetNull);

        // DayCounter keyed por fecha
        modelBuilder.Entity<DayCounter>()
            .HasKey(dc => dc.ServiceDate);
    }
}
