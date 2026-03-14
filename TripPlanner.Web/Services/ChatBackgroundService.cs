using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Data;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.Services;

/// <summary>
/// Background service that processes pending chat jobs by calling the Ollama AI service
/// in a long-running hosted task. This prevents Blazor SignalR circuit timeouts caused
/// by awaiting slow LLM inference directly in a component.
/// </summary>
public partial class ChatBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ChatBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(3);

    // Maximum number of chat jobs processed per polling cycle.
    private const int MaxJobsPerCycle = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ChatBackgroundService started.");

        // Reset any jobs left in Processing state from a previous run (e.g. after a crash).
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
                logger.LogError(ex, "Unexpected error in ChatBackgroundService polling loop.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        logger.LogInformation("ChatBackgroundService stopped.");
    }

    private async Task ResetStuckProcessingJobsAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stuck = await context.ChatJobs
                .Where(j => j.Status == ChatJobStatus.Processing)
                .ToListAsync();
            if (stuck.Count > 0)
            {
                foreach (var job in stuck)
                    job.Status = ChatJobStatus.Pending;
                await context.SaveChangesAsync();
                logger.LogWarning("Reset {Count} stuck Processing chat job(s) to Pending on startup.", stuck.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reset stuck chat jobs on startup.");
        }
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IChatJobRepository>();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();

        var pendingJobs = await jobRepo.GetPendingJobsAsync(MaxJobsPerCycle);

        foreach (var job in pendingJobs)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await ProcessJobAsync(job, jobRepo, chatService, userManager, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(
        ChatJob job,
        IChatJobRepository jobRepo,
        IChatService chatService,
        Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        LogProcessingChatJob(job.Id, job.ConversationId);

        job.Status = ChatJobStatus.Processing;
        await jobRepo.UpdateAsync(job);

        try
        {
            // Apply the user's persisted home location so the system prompt includes
            // GPS coordinates and the get_weather tool hint, matching the browser path.
            var user = await userManager.FindByIdAsync(job.UserId);
            if (user?.HomeLatitude is not null && user.HomeLongitude is not null)
                chatService.SetUserLocation(user.HomeLatitude.Value, user.HomeLongitude.Value);
            if (!string.IsNullOrWhiteSpace(user?.PreferredLanguage))
                chatService.SetUserLanguage(user.PreferredLanguage);

            // Load the conversation history from the database (which already contains the user message).
            var loaded = await chatService.LoadConversationAsync(job.ConversationId, job.UserId);
            if (!loaded)
            {
                job.Status = ChatJobStatus.Failed;
                job.ErrorMessage = "Conversation not found.";
                job.CompletedAt = DateTimeOffset.UtcNow;
                await jobRepo.UpdateAsync(job);
                LogConversationNotFound(job.Id, job.ConversationId);
                return;
            }

            // Run the Ollama inference loop on the loaded history.
            await chatService.RunInferenceAsync(job.UserId, cancellationToken);

            job.Status = ChatJobStatus.Completed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await jobRepo.UpdateAsync(job);

            LogJobCompleted(job.Id);
        }
        catch (OperationCanceledException)
        {
            // Revert to Pending so the job can be retried after restart.
            job.Status = ChatJobStatus.Pending;
            await jobRepo.UpdateAsync(job);
            throw;
        }
        catch (Exception ex)
        {
            job.Status = ChatJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            await jobRepo.UpdateAsync(job);
            logger.LogWarning(ex, "Chat job {JobId} failed.", job.Id);
        }
    }


    [LoggerMessage(Level = LogLevel.Trace, Message = "Processing chat job {JobId} for conversation {ConversationId}")]
    private partial void LogProcessingChatJob(string jobId, string conversationId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Chat job {JobId} completed successfully.")]
    private partial void LogJobCompleted(string jobId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Chat job {JobId}: conversation {ConversationId} not found.")]
    private partial void LogConversationNotFound(string jobId, string conversationId);
}
