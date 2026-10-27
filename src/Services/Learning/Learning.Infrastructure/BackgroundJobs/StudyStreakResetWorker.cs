using Learning.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Learning.Infrastructure.BackgroundJobs;

public class StudyStreakResetWorker(IServiceProvider serviceProvider, ILogger<StudyStreakResetWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StudyStreakResetWorker background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var gamificationService = scope.ServiceProvider.GetRequiredService<GamificationService>();
                await gamificationService.ResetExpiredStreaksAsync(stoppingToken);
                logger.LogDebug("Checked and updated expired study streaks.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during daily study streak reset check.");
            }

            // Runs every 1 hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
