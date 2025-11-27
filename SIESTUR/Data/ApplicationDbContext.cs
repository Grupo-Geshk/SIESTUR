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

        // ===== WINDOW =====
        modelBuilder.Entity<Window>()
            .HasIndex(w => w.Number)
            .IsUnique();

        // ===== TURN INDEXES =====
        // Critical index for PENDING queue lookup with FIFO ordering
        modelBuilder.Entity<Turn>()
            .HasIndex(t => new { t.Status, t.Number, t.CreatedAt });

        // Index for date-based filtering
        modelBuilder.Entity<Turn>()
            .HasIndex(t => t.CreatedAt);

        // Index for window-based queries
        modelBuilder.Entity<Turn>()
            .HasIndex(t => new { t.WindowId, t.Status });

        // ===== USER INDEXES =====
        // Unique case-insensitive email index for login
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // ===== WORKER SESSION INDEXES =====
        // Index for finding active sessions by user
        modelBuilder.Entity<WorkerSession>()
            .HasIndex(ws => new { ws.UserId, ws.EndedAt });

        // Index for finding active sessions by window
        modelBuilder.Entity<WorkerSession>()
            .HasIndex(ws => new { ws.WindowId, ws.EndedAt });

        // ===== VIDEO INDEXES =====
        // Index for ordering video playlist
        modelBuilder.Entity<Video>()
            .HasIndex(v => v.Position);

        // ===== FOREIGN KEY RELATIONSHIPS =====
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

        // ===== KEYS =====
        // DayCounter keyed por fecha
        modelBuilder.Entity<DayCounter>()
            .HasKey(dc => dc.ServiceDate);
    }
}
