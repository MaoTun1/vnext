using System;
using System.Text.Json;
using System.Threading.Tasks;
using BBT.Aether.Testing;
using Shouldly;
using Xunit;

namespace BBT.Workflow.Instances;

/// <summary>
/// Base test class for testing IInstanceJobRepository implementations.
/// Contains test methods that verify the repository contract and behavior.
/// </summary>
public abstract class InstanceJobRepositoryTests<TEntry> : DomainTestBase<TEntry>
    where TEntry : ModuleEntryPointBase, new()
{
    protected IInstanceJobRepository Repository => GetRequiredService<IInstanceJobRepository>();

    /// <summary>
    /// Tests that InsertAsync successfully creates a new instance job in the database.
    /// </summary>
    [Fact]
    public async Task InsertAsync_ShouldCreateJob()
    {
        // Arrange
        var job = InstanceJob.Create(
            Guid.NewGuid(),
            "test-job",
            "test-job-1",
            "test",
            "test-workflow",
            Guid.NewGuid(),
            "0 0 * * *",
            JsonDocument.Parse("{}").RootElement
        );

        // Act
        var result = await Repository.InsertAsync(job);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(job.Id);
        result.JobId.ShouldBe("test-job-1");
        result.JobName.ShouldBe("test-job");
    }

    /// <summary>
    /// Tests that ExistsAsync returns true when a job exists.
    /// </summary>
    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueWhenExists()
    {
        // Arrange
        var job = InstanceJob.Create(
            Guid.NewGuid(),
            "test-job-exists",
            "test-job-exists-id",
            "test",
            "test-workflow",
            Guid.NewGuid(),
            "0 0 * * *",
            JsonDocument.Parse("{}").RootElement
        );
        await Repository.InsertAsync(job);

        // Act
        var result = await Repository.ExistsAsync("test-job-exists-id");

        // Assert
        result.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that ExistsAsync returns false when a job does not exist.
    /// </summary>
    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseWhenNotExists()
    {
        // Act
        var result = await Repository.ExistsAsync("non-existent-job");

        // Assert
        result.ShouldBeFalse();
    }

    /// <summary>
    /// Tests that FindByNameAsync returns the job when it exists.
    /// </summary>
    [Fact]
    public async Task FindByNameAsync_ShouldReturnJobWhenExists()
    {
        // Arrange
        var job = InstanceJob.Create(
            Guid.NewGuid(),
            "test-job-find",
            "test-job-find-id",
            "test",
            "test-workflow",
            Guid.NewGuid(),
            "0 0 * * *",
            JsonDocument.Parse("{}").RootElement
        );
        await Repository.InsertAsync(job);

        // Act
        var result = await Repository.FindByNameAsync("test-job-find-id");

        // Assert
        result.ShouldNotBeNull();
        result.JobId.ShouldBe("test-job-find-id");
        result.JobName.ShouldBe("test-job-find");
    }

    /// <summary>
    /// Tests that FindByNameAsync returns null when the job does not exist.
    /// </summary>
    [Fact]
    public async Task FindByNameAsync_ShouldReturnNullWhenNotExists()
    {
        // Act
        var result = await Repository.FindByNameAsync("non-existent-job");

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// Tests that GetListUntriggeredAsync returns only untriggered jobs.
    /// </summary>
    [Fact]
    public async Task GetListUntriggeredAsync_ShouldReturnOnlyUntriggeredJobs()
    {
        // Arrange
        var untriggeredJob1 = InstanceJob.Create(
            Guid.NewGuid(),
            "untriggered-job-1",
            "untriggered-1",
            "test",
            "test-workflow",
            Guid.NewGuid(),
            "0 0 * * *",
            JsonDocument.Parse("{}").RootElement
        );
        
        var untriggeredJob2 = InstanceJob.Create(
            Guid.NewGuid(),
            "untriggered-job-2",
            "untriggered-2",
            "test",
            "test-workflow",
            Guid.NewGuid(),
            "0 0 * * *",
            JsonDocument.Parse("{}").RootElement
        );
        
        var triggeredJob = InstanceJob.Create(
            Guid.NewGuid(),
            "triggered-job-1",
            "triggered-1",
            "test",
            "test-workflow",
            Guid.NewGuid(),
            "0 0 * * *",
            JsonDocument.Parse("{}").RootElement
        );

        await Repository.InsertAsync(untriggeredJob1);
        await Repository.InsertAsync(untriggeredJob2);
        await Repository.InsertAsync(triggeredJob);
        
        // Mark the triggered job as triggered
        triggeredJob.Triggered();
        await Repository.UpdateAsync(triggeredJob);

        // Act
        var results = await Repository.GetListUntriggeredAsync();

        // Assert
        results.ShouldNotBeNull();
        results.Count.ShouldBeGreaterThanOrEqualTo(2);
        results.ShouldContain(j => j.JobId == "untriggered-1");
        results.ShouldContain(j => j.JobId == "untriggered-2");
    }

    /// <summary>
    /// Tests that UpdateAsync successfully marks a job as triggered.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldMarkJobAsTriggered()
    {
        // Arrange
        var job = InstanceJob.Create(
            Guid.NewGuid(),
            "test-job-update",
            "test-job-update-id",
            "test",
            "test-workflow",
            Guid.NewGuid(),
            "0 0 * * *",
            JsonDocument.Parse("{}").RootElement
        );
        await Repository.InsertAsync(job);

        // Act
        job.Triggered();
        var result = await Repository.UpdateAsync(job);

        // Assert
        result.ShouldNotBeNull();
        result.IsTriggered.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that DeleteAsync successfully removes a job from the database.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldRemoveJob()
    {
        // Arrange
        var job = InstanceJob.Create(
            Guid.NewGuid(),
            "test-job-delete",
            "test-job-delete-id",
            "test",
            "test-workflow",
            Guid.NewGuid(),
            "0 0 * * *",
            JsonDocument.Parse("{}").RootElement
        );
        await Repository.InsertAsync(job);

        // Act
        await Repository.DeleteAsync(job);

        // Assert
        var deleted = await Repository.FindAsync(job.Id);
        deleted.ShouldBeNull();
    }
}

