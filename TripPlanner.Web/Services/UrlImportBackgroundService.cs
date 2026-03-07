using TripPlanner.Web.Data;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;
using TripPlanner.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace TripPlanner.Web.Services;

/// <summary>
/// Background service that processes pending URL import jobs by analyzing each URL
/// with the AI place analysis service and creating a Place (flagged NeedsReview) for each result.
/// </summary>
public class UrlImportBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<UrlImportBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);

    // Maximum number of jobs processed per polling cycle.
    // Keeps individual cycles short so the service stays responsive to cancellation.
    // Increase via configuration if higher throughput is needed.
    private const int MaxJobsPerCycle = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("UrlImportBackgroundService started.");

        // Reset any jobs left in Processing state from a previous run (e.g. after a crash)
        await ResetStuckProcessingJobsAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in UrlImportBackgroundService polling loop.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        logger.LogInformation("UrlImportBackgroundService stopped.");
    }

    private async Task ResetStuckProcessingJobsAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TripPlanner.Web.Data.ApplicationDbContext>();
            var stuck = await context.UrlImportJobs
                .Where(j => j.Status == UrlImportJobStatus.Processing)
                .ToListAsync();
            if (stuck.Count > 0)
            {
                foreach (var job in stuck)
                    job.Status = UrlImportJobStatus.Pending;
                await context.SaveChangesAsync();
                logger.LogWarning("Reset {Count} stuck Processing job(s) to Pending on startup.", stuck.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reset stuck Processing jobs on startup.");
        }
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IUrlImportJobRepository>();
        var placeRepo = scope.ServiceProvider.GetRequiredService<IPlaceRepository>();
        var analysisService = scope.ServiceProvider.GetRequiredService<IPlaceAnalysisService>();

        var pendingJobs = await jobRepo.GetPendingJobsAsync(maxCount: MaxJobsPerCycle);

        foreach (var job in pendingJobs)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await ProcessJobAsync(job, jobRepo, placeRepo, analysisService, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(
        UrlImportJob job,
        IUrlImportJobRepository jobRepo,
        IPlaceRepository placeRepo,
        IPlaceAnalysisService analysisService,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing URL import job {JobId} for URL: {Url}", job.Id, job.Url);

        // Mark as Processing
        job.Status = UrlImportJobStatus.Processing;
        await jobRepo.UpdateAsync(job);

        try
        {
            var suggestion = await analysisService.AnalyzeUrlAsync(job.Url, cancellationToken);

            if (suggestion == null)
            {
                job.Status = UrlImportJobStatus.Failed;
                job.ErrorMessage = "The AI analysis returned no result for this URL.";
                job.ProcessedAt = DateTime.UtcNow;
                await jobRepo.UpdateAsync(job);
                logger.LogWarning("Job {JobId}: analysis returned null for URL {Url}", job.Id, job.Url);
                return;
            }

            var place = new Place
            {
                Name = suggestion.Name ?? job.Url,
                Description = suggestion.Description ?? string.Empty,
                Category = Enum.TryParse<PlaceCategory>(suggestion.Category, true, out var cat) ? cat : PlaceCategory.Other,
                Latitude = suggestion.Latitude ?? 0,
                Longitude = suggestion.Longitude ?? 0,
                Tags = suggestion.Tags,
                WishlistId = job.WishlistId,
                Url = job.Url,
                NeedsReview = true,
                CreatedAt = DateTime.UtcNow
            };

            var created = await placeRepo.AddAsync(place);

            job.Status = UrlImportJobStatus.Completed;
            job.CreatedPlaceId = created.Id;
            job.ProcessedAt = DateTime.UtcNow;
            job.ErrorMessage = null;
            await jobRepo.UpdateAsync(job);

            logger.LogInformation("Job {JobId}: created place {PlaceId} ({PlaceName}) from URL {Url}",
                job.Id, created.Id, created.Name, job.Url);
        }
        catch (OperationCanceledException)
        {
            // Revert to Pending so it can be retried
            job.Status = UrlImportJobStatus.Pending;
            await jobRepo.UpdateAsync(job);
            throw;
        }
        catch (Exception ex)
        {
            job.Status = UrlImportJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.ProcessedAt = DateTime.UtcNow;
            await jobRepo.UpdateAsync(job);
            logger.LogWarning(ex, "Job {JobId}: failed to process URL {Url}", job.Id, job.Url);
        }
    }
}
