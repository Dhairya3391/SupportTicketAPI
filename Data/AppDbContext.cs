using Microsoft.EntityFrameworkCore;
using SupportTicketAPI.Models;

namespace SupportTicketAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketStatusLog> TicketStatusLogs => Set<TicketStatusLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Role ──────────────────────────────────────────────
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(r => r.Name).HasColumnName("name").IsRequired();
            entity.HasIndex(r => r.Name).IsUnique();
        });

        // ── User ──────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(u => u.Name).HasColumnName("name").IsRequired().HasMaxLength(255);
            entity.Property(u => u.Email).HasColumnName("email").IsRequired().HasMaxLength(255);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Password).HasColumnName("password").IsRequired().HasMaxLength(255);
            entity.Property(u => u.RoleId).HasColumnName("role_id");
            entity.Property(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            entity.HasOne(u => u.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(u => u.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Ticket ────────────────────────────────────────────
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("tickets");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(t => t.Title).HasColumnName("title").IsRequired().HasMaxLength(255);
            entity.Property(t => t.Description).HasColumnName("description").IsRequired();
            entity.Property(t => t.Status).HasColumnName("status").IsRequired().HasDefaultValue("OPEN");
            entity.Property(t => t.Priority).HasColumnName("priority").IsRequired().HasDefaultValue("MEDIUM");
            entity.Property(t => t.CreatedBy).HasColumnName("created_by");
            entity.Property(t => t.AssignedTo).HasColumnName("assigned_to").IsRequired(false);
            entity.Property(t => t.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            // created_by → users
            entity.HasOne(t => t.Creator)
                  .WithMany(u => u.CreatedTickets)
                  .HasForeignKey(t => t.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            // assigned_to → users (nullable)
            entity.HasOne(t => t.Assignee)
                  .WithMany(u => u.AssignedTickets)
                  .HasForeignKey(t => t.AssignedTo)
                  .OnDelete(DeleteBehavior.SetNull)
                  .IsRequired(false);
        });

        // ── TicketComment ─────────────────────────────────────
        modelBuilder.Entity<TicketComment>(entity =>
        {
            entity.ToTable("ticket_comments");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(c => c.TicketId).HasColumnName("ticket_id");
            entity.Property(c => c.UserId).HasColumnName("user_id");
            entity.Property(c => c.Comment).HasColumnName("comment").IsRequired();
            entity.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");

            entity.HasOne(c => c.Ticket)
                  .WithMany(t => t.Comments)
                  .HasForeignKey(c => c.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.User)
                  .WithMany(u => u.Comments)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── TicketStatusLog ───────────────────────────────────
        modelBuilder.Entity<TicketStatusLog>(entity =>
        {
            entity.ToTable("ticket_status_logs");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(l => l.TicketId).HasColumnName("ticket_id");
            entity.Property(l => l.OldStatus).HasColumnName("old_status").IsRequired();
            entity.Property(l => l.NewStatus).HasColumnName("new_status").IsRequired();
            entity.Property(l => l.ChangedBy).HasColumnName("changed_by");
            entity.Property(l => l.ChangedAt).HasColumnName("changed_at").HasDefaultValueSql("NOW()");

            entity.HasOne(l => l.Ticket)
                  .WithMany(t => t.StatusLogs)
                  .HasForeignKey(l => l.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Changer)
                  .WithMany(u => u.StatusLogs)
                  .HasForeignKey(l => l.ChangedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Seed Roles ────────────────────────────────────────
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "MANAGER" },
            new Role { Id = 2, Name = "SUPPORT" },
            new Role { Id = 3, Name = "USER" }
        );
    }
}
