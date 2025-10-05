using EDiscovery.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EDiscoveryIntakeApi.Data;

public class EDiscoveryDbContext : DbContext
{
    public EDiscoveryDbContext(DbContextOptions<EDiscoveryDbContext> options) : base(options)
    {
    }

    public DbSet<Matter> Matters { get; set; }
    public DbSet<CollectionJob> CollectionJobs { get; set; }
    public DbSet<CollectedItem> CollectedItems { get; set; }
    public DbSet<JobLog> JobLogs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<JobAssignment> JobAssignments { get; set; }
    public DbSet<WorkerInstance> WorkerInstances { get; set; }
    public DbSet<JobShard> JobShards { get; set; }
    public DbSet<JobShardCheckpoint> JobShardCheckpoints { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure relationships
        modelBuilder.Entity<CollectionJob>()
            .HasOne(j => j.Matter)
            .WithMany(m => m.CollectionJobs)
            .HasForeignKey(j => j.MatterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CollectedItem>()
            .HasOne(i => i.Job)
            .WithMany(j => j.CollectedItems)
            .HasForeignKey(i => i.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JobLog>()
            .HasOne(l => l.Job)
            .WithMany(j => j.JobLogs)
            .HasForeignKey(l => l.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure User relationships
        modelBuilder.Entity<CollectionJob>()
            .HasOne(j => j.AssignedUser)
            .WithMany(u => u.AssignedJobs)
            .HasForeignKey(j => j.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Matter>()
            .HasOne(m => m.CreatedByUser)
            .WithMany(u => u.CreatedMatters)
            .HasForeignKey(m => m.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<UserSession>()
            .HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JobAssignment>()
            .HasOne(a => a.Job)
            .WithMany()
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JobAssignment>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure indexes for performance and concurrency
        modelBuilder.Entity<CollectionJob>()
            .HasIndex(j => j.CustodianEmail);

        modelBuilder.Entity<CollectionJob>()
            .HasIndex(j => j.Status);

        modelBuilder.Entity<CollectionJob>()
            .HasIndex(j => new { j.Status, j.Priority, j.CreatedDate });

        modelBuilder.Entity<CollectionJob>()
            .HasIndex(j => new { j.AssignedUserId, j.Status });

        modelBuilder.Entity<CollectionJob>()
            .HasIndex(j => j.LockExpiry);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<UserSession>()
            .HasIndex(s => s.SessionId)
            .IsUnique();

        modelBuilder.Entity<UserSession>()
            .HasIndex(s => new { s.UserId, s.IsActive });

        modelBuilder.Entity<JobAssignment>()
            .HasIndex(a => new { a.JobId, a.WorkerId });

        modelBuilder.Entity<JobAssignment>()
            .HasIndex(a => new { a.UserId, a.Status });

        modelBuilder.Entity<JobAssignment>()
            .HasIndex(a => a.LockExpiry);

        modelBuilder.Entity<WorkerInstance>()
            .HasIndex(w => w.WorkerId)
            .IsUnique();

        modelBuilder.Entity<WorkerInstance>()
            .HasIndex(w => new { w.Status, w.LastHeartbeat });

        // Job Shard configurations
        modelBuilder.Entity<JobShard>()
            .HasOne(s => s.ParentJob)
            .WithMany()
            .HasForeignKey(s => s.ParentJobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JobShard>()
            .HasOne(s => s.AssignedUser)
            .WithMany()
            .HasForeignKey(s => s.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<JobShard>()
            .HasIndex(s => s.Status);

        modelBuilder.Entity<JobShard>()
            .HasIndex(s => new { s.ParentJobId, s.ShardIndex });

        modelBuilder.Entity<JobShard>()
            .HasIndex(s => new { s.Status, s.CreatedDate });

        modelBuilder.Entity<JobShard>()
            .HasIndex(s => s.LockExpiry);

        modelBuilder.Entity<JobShard>()
            .HasIndex(s => s.ShardIdentifier)
            .IsUnique();

        modelBuilder.Entity<JobShard>()
            .HasIndex(s => new { s.CustodianEmail, s.StartDate, s.EndDate });

        // Job Shard Checkpoint configurations
        modelBuilder.Entity<JobShardCheckpoint>()
            .HasOne(c => c.JobShard)
            .WithMany(s => s.Checkpoints)
            .HasForeignKey(c => c.JobShardId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<JobShardCheckpoint>()
            .HasIndex(c => new { c.JobShardId, c.CheckpointKey })
            .IsUnique();

        modelBuilder.Entity<JobShardCheckpoint>()
            .HasIndex(c => new { c.JobShardId, c.IsCompleted });

        modelBuilder.Entity<JobShardCheckpoint>()
            .HasIndex(c => c.CorrelationId);

        modelBuilder.Entity<CollectedItem>()
            .HasIndex(i => i.ItemId);

        modelBuilder.Entity<CollectedItem>()
            .HasIndex(i => i.Sha256Hash);

        modelBuilder.Entity<JobLog>()
            .HasIndex(l => l.Timestamp);

        // Seed data for POC
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Username = "admin",
                Email = "admin@company.com",
                FirstName = "System",
                LastName = "Administrator",
                Role = UserRole.Administrator,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                MaxConcurrentJobs = 10,
                MaxDataSizePerJobGB = 500,
                Department = "IT",
                Location = "HQ"
            },
            new User
            {
                Id = 2,
                Username = "analyst1",
                Email = "analyst1@company.com",
                FirstName = "Jane",
                LastName = "Smith",
                Role = UserRole.Analyst,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                MaxConcurrentJobs = 5,
                MaxDataSizePerJobGB = 100,
                Department = "Legal",
                Location = "HQ"
            }
        );

        modelBuilder.Entity<Matter>().HasData(
            new Matter
            {
                Id = 1,
                Name = "Sample Investigation",
                Description = "POC investigation for testing hybrid collection",
                CaseNumber = "CASE-2024-001",
                CreatedBy = "admin@company.com",
                CreatedByUserId = 1,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            }
        );
    }
}