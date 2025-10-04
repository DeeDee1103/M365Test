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

        // Configure indexes for performance
        modelBuilder.Entity<CollectionJob>()
            .HasIndex(j => j.CustodianEmail);

        modelBuilder.Entity<CollectionJob>()
            .HasIndex(j => j.Status);

        modelBuilder.Entity<CollectedItem>()
            .HasIndex(i => i.ItemId);

        modelBuilder.Entity<CollectedItem>()
            .HasIndex(i => i.Sha256Hash);

        modelBuilder.Entity<JobLog>()
            .HasIndex(l => l.Timestamp);

        // Seed data for POC
        modelBuilder.Entity<Matter>().HasData(
            new Matter
            {
                Id = 1,
                Name = "Sample Investigation",
                Description = "POC investigation for testing hybrid collection",
                CaseNumber = "CASE-2024-001",
                CreatedBy = "admin@company.com",
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            }
        );
    }
}