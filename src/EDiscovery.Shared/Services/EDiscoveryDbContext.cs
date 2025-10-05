using EDiscovery.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EDiscovery.Shared.Services;

/// <summary>
/// Database context for the eDiscovery system with multi-user concurrent processing support
/// </summary>
public class EDiscoveryDbContext : DbContext
{
    public EDiscoveryDbContext(DbContextOptions<EDiscoveryDbContext> options) : base(options)
    {
    }

    // Core entities
    public DbSet<Matter> Matters { get; set; }
    public DbSet<CollectionJob> CollectionJobs { get; set; }
    public DbSet<CollectedItem> CollectedItems { get; set; }
    public DbSet<JobLog> JobLogs { get; set; }

    // Multi-user concurrent processing entities
    public DbSet<User> Users { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<JobAssignment> JobAssignments { get; set; }
    public DbSet<WorkerInstance> WorkerInstances { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Matter entity
        modelBuilder.Entity<Matter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.Name);
            
            // Relationship with User (creator)
            entity.HasOne(e => e.CreatedByUser)
                  .WithMany(u => u.CreatedMatters)
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure CollectionJob entity
        modelBuilder.Entity<CollectionJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustodianEmail).IsRequired().HasMaxLength(200);
            entity.Property(e => e.JobType).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.CustodianEmail);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Status, e.Priority, e.CreatedDate });
            
            // Relationship with Matter
            entity.HasOne(e => e.Matter)
                  .WithMany(m => m.CollectionJobs)
                  .HasForeignKey(e => e.MatterId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            // Relationship with User (assigned user)
            entity.HasOne(e => e.AssignedUser)
                  .WithMany(u => u.AssignedJobs)
                  .HasForeignKey(e => e.AssignedUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure CollectedItem entity
        modelBuilder.Entity<CollectedItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.From).HasMaxLength(200);
            entity.Property(e => e.To).HasMaxLength(1000);
            entity.Property(e => e.Sha256Hash).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => e.Sha256Hash).IsUnique();
            entity.HasIndex(e => e.CollectedDate);
            
            // Relationship with CollectionJob
            entity.HasOne(e => e.Job)
                  .WithMany(j => j.CollectedItems)
                  .HasForeignKey(e => e.JobId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure JobLog entity
        modelBuilder.Entity<JobLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Level).HasConversion<string>();
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Level);
            
            // Relationship with CollectionJob
            entity.HasOne(e => e.Job)
                  .WithMany(j => j.JobLogs)
                  .HasForeignKey(e => e.JobId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Role).HasConversion<string>();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Role);
            entity.HasIndex(e => e.IsActive);
        });

        // Configure UserSession entity
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.HasIndex(e => e.SessionId).IsUnique();
            entity.HasIndex(e => e.LastActivityTime);
            entity.HasIndex(e => e.IsActive);
            
            // Relationship with User
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Sessions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure JobAssignment entity
        modelBuilder.Entity<JobAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkerId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LockToken).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.WorkerId);
            entity.HasIndex(e => e.LockToken).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.HeartbeatTime);
            entity.HasIndex(e => new { e.JobId, e.Status });
            
            // Relationship with CollectionJob
            entity.HasOne(e => e.Job)
                  .WithMany()
                  .HasForeignKey(e => e.JobId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            // Relationship with User
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure WorkerInstance entity
        modelBuilder.Entity<WorkerInstance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkerId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MachineName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.Version).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.WorkerId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.LastHeartbeat);
        });

        // Seed data for default users
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "admin",
                Email = "admin@ediscovery.local",
                FirstName = "System",
                LastName = "Administrator",
                Role = UserRole.Administrator,
                MaxConcurrentJobs = 10,
                MaxDataSizePerJobGB = 100,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            },
            new User
            {
                Id = 2,
                Username = "analyst",
                Email = "analyst@ediscovery.local",
                FirstName = "eDiscovery",
                LastName = "Analyst",
                Role = UserRole.Analyst,
                MaxConcurrentJobs = 5,
                MaxDataSizePerJobGB = 50,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            }
        );
    }
}