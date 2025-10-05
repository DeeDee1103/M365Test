using EDiscovery.Shared.Models;
using Xunit;

namespace EDiscovery.Shared.Tests.Models;

public class MatterTests
{
    [Fact]
    public void Matter_DefaultValues_AreSetCorrectly()
    {
        // Act
        var matter = new Matter();

        // Assert
        Assert.Equal(0, matter.Id);
        Assert.Equal(string.Empty, matter.CaseNumber);
        Assert.Equal(string.Empty, matter.Name);
        Assert.Null(matter.Description);
        Assert.NotEqual(DateTime.MinValue, matter.CreatedDate);
        Assert.True(matter.IsActive);
        Assert.Empty(matter.CollectionJobs);
    }

    [Fact]
    public void Matter_WithProperties_SetsValuesCorrectly()
    {
        // Arrange
        var caseNumber = "CASE-001";
        var name = "Test Matter";
        var description = "Test Description";
        var createdBy = "test@company.com";
        var isActive = false;

        // Act
        var matter = new Matter
        {
            CaseNumber = caseNumber,
            Name = name,
            Description = description,
            CreatedBy = createdBy,
            IsActive = isActive
        };

        // Assert
        Assert.Equal(caseNumber, matter.CaseNumber);
        Assert.Equal(name, matter.Name);
        Assert.Equal(description, matter.Description);
        Assert.Equal(createdBy, matter.CreatedBy);
        Assert.Equal(isActive, matter.IsActive);
    }
}

public class CollectionJobTests
{
    [Fact]
    public void CollectionJob_DefaultValues_AreSetCorrectly()
    {
        // Act
        var job = new CollectionJob();

        // Assert
        Assert.Equal(0, job.Id);
        Assert.Equal(0, job.MatterId);
        Assert.Null(job.Matter);
        Assert.Equal(string.Empty, job.CustodianEmail);
        Assert.Equal(CollectionJobType.Email, job.JobType);
        Assert.Equal(CollectionJobStatus.Pending, job.Status);
        Assert.Equal(CollectionRoute.GraphApi, job.Route);
        Assert.NotEqual(DateTime.MinValue, job.CreatedDate);
        Assert.Empty(job.CollectedItems);
        Assert.Empty(job.JobLogs);
    }

    [Fact]
    public void CollectionJob_WithProperties_SetsValuesCorrectly()
    {
        // Arrange
        var matterId = 1;
        var custodianEmail = "test@company.com";
        var jobType = CollectionJobType.OneDrive;
        var status = CollectionJobStatus.Running;
        var route = CollectionRoute.GraphDataConnect;

        // Act
        var job = new CollectionJob
        {
            MatterId = matterId,
            CustodianEmail = custodianEmail,
            JobType = jobType,
            Status = status,
            Route = route
        };

        // Assert
        Assert.Equal(matterId, job.MatterId);
        Assert.Equal(custodianEmail, job.CustodianEmail);
        Assert.Equal(jobType, job.JobType);
        Assert.Equal(status, job.Status);
        Assert.Equal(route, job.Route);
    }
}

public class CollectedItemTests
{
    [Fact]
    public void CollectedItem_DefaultValues_AreSetCorrectly()
    {
        // Act
        var item = new CollectedItem();

        // Assert
        Assert.Equal(0, item.Id);
        Assert.Equal(0, item.JobId);
        Assert.Null(item.Job);
        Assert.Equal(string.Empty, item.ItemId);
        Assert.Equal(string.Empty, item.ItemType);
        Assert.Null(item.Subject);
        Assert.Equal(string.Empty, item.Sha256Hash);
        Assert.Equal(0, item.SizeBytes);
        Assert.NotEqual(DateTime.MinValue, item.CollectedDate);
        Assert.Null(item.FilePath);
    }

    [Fact]
    public void CollectedItem_WithProperties_SetsValuesCorrectly()
    {
        // Arrange
        var jobId = 1;
        var itemId = "email-123";
        var itemType = "Email";
        var subject = "Test Email";
        var sha256Hash = "abc123hash";
        var sizeBytes = 1024L;
        var collectedDate = DateTime.UtcNow;
        var filePath = "/data/email-123.msg";

        // Act
        var item = new CollectedItem
        {
            JobId = jobId,
            ItemId = itemId,
            ItemType = itemType,
            Subject = subject,
            Sha256Hash = sha256Hash,
            SizeBytes = sizeBytes,
            CollectedDate = collectedDate,
            FilePath = filePath
        };

        // Assert
        Assert.Equal(jobId, item.JobId);
        Assert.Equal(itemId, item.ItemId);
        Assert.Equal(itemType, item.ItemType);
        Assert.Equal(subject, item.Subject);
        Assert.Equal(sha256Hash, item.Sha256Hash);
        Assert.Equal(sizeBytes, item.SizeBytes);
        Assert.Equal(collectedDate, item.CollectedDate);
        Assert.Equal(filePath, item.FilePath);
    }

    [Fact]
    public void CollectedItem_Sha256Hash_AcceptsValidHash()
    {
        // Arrange
        var validSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var item = new CollectedItem();

        // Act
        item.Sha256Hash = validSha256;

        // Assert
        Assert.Equal(validSha256, item.Sha256Hash);
    }
}