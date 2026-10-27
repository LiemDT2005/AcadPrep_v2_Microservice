using System.Security.Claims;
using Exam.Controllers;
using Exam.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Exam.Tests;

public class ExamServiceTests
{
    [Fact]
    public void MappingConfiguration_IsValid() => TestServices.MappingConfiguration.AssertConfigurationIsValid();

    [Fact]
    public async Task Attempts_CountsAllAttempts_ButHistoryUsesAuthenticatedUserOnly()
    {
        using var db = await ExamSessionTests.CreateDb();
        db.ExamAttempts.AddRange(
            new() { Id = 1, ExamId = 1, UserId = "learner", StartedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = 2, ExamId = 1, UserId = "learner", StartedAt = DateTime.UtcNow.AddDays(-1),
                IsSubmitted = true, ListeningScore = 300, ReadingScore = 400, TotalScore = 700 },
            new() { Id = 3, ExamId = 1, UserId = "someone-else" });
        await db.SaveChangesAsync();
        var controller = new ExamAttemptsController(TestServices.CreateAttemptService(db))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "learner") }, "test"))
                }
            }
        };
        // A query-string userId must never select another learner's history.
        controller.Request.QueryString = new QueryString("?userId=someone-else");
        Assert.Equal(3, Assert.IsType<OkObjectResult>(await controller.GetCount(1, default)).Value);
        var history = Assert.IsType<List<UserAttemptHistoryDto>>(
            Assert.IsType<OkObjectResult>(await controller.GetMyHistory(1, default)).Value);
        Assert.Equal(new[] { 2, 1 }, history.Select(a => a.AttemptId));
        Assert.Equal(700, history[0].TotalScore);
        Assert.True(history[0].IsSubmitted);
        Assert.Empty(await TestServices.CreateAttemptService(db).GetHistoryAsync(1, "unknown"));
        Assert.Equal(0, await TestServices.CreateAttemptService(db).GetAttemptCountAsync(999));
    }

    [Fact]
    public async Task History_RejectsRequestsWithoutUserId()
    {
        using var db = await ExamSessionTests.CreateDb();
        var controller = new ExamAttemptsController(TestServices.CreateAttemptService(db))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        Assert.IsType<UnauthorizedResult>(await controller.GetMyHistory(1, default));
    }
}
