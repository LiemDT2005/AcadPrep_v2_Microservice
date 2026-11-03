using Exam.Data;
using Exam.DTOs;
using Exam.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Messaging;

public class ExamExpiryWorker(IServiceScopeFactory scopes, ILogger<ExamExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ExamDbContext>();
                var expired = await db.SessionStates.AsNoTracking()
                    .Where(s => !s.IsSubmitted && s.ExpiresAt != null && s.ExpiresAt <= DateTime.UtcNow)
                    .Select(s => new { s.Id, s.UserId }).Take(50).ToListAsync(stoppingToken);
                foreach (var state in expired)
                {
                    using var submissionScope = scopes.CreateScope();
                    var lifecycle = submissionScope.ServiceProvider.GetRequiredService<IExamLifecycleService>();
                    var key = state.Id.Split(':');
                    await lifecycle.SubmitAsync(int.Parse(key[1]), key[0], state.UserId, new SaveSessionRequest([]), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (DbUpdateConcurrencyException) { /* The learner submitted at the deadline. Retry next sweep. */ }
            catch (Exception ex) { logger.LogWarning(ex, "Expired sessions will be submitted on the next sweep."); }
        }
    }
}
