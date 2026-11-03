using Content.Controllers;
using Content.DTOs;
using Content.Entities.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Content.Tests;

public class ContentServiceTests
{
    [Fact]
    public void MappingConfiguration_IsValid() => ContentTestContext.MappingConfiguration.AssertConfigurationIsValid();

    [Theory]
    [InlineData(null, "Published", null, 1)]
    [InlineData(null, "Draft", null, 2)]
    [InlineData(null, "Hidden", null, 4, 3)]
    [InlineData("  GRAMMAR  ", null, null, 1)]
    [InlineData(null, "All", "Practice", 2)]
    [InlineData(null, "unknown", "All", 4, 3, 2, 1)]
    public async Task List_FiltersAndProjectsBeforeAddingAttemptCounts(
        string? keyword, string? status, string? examType, params int[] expectedIds)
    {
        await using var context = await ContentTestContext.CreateAsync();
        var result = await context.Service.GetPagedExamsAsync(keyword, status, examType, 1, 10);
        Assert.True(result.IsSuccess);
        var page = Assert.IsType<PagedResultDto<ExamListItemDto>>(result.Value);
        Assert.Equal(expectedIds, page.Items.Select(e => e.Id));
        Assert.Equal(expectedIds.Length, page.TotalCount);
        foreach (var item in page.Items)
            Assert.Equal(context.Attempts.Counts.GetValueOrDefault(item.Id), item.AttemptCount);
        if (page.Items.Any(e => e.Id == 1))
        {
            var item = Assert.Single(page.Items, e => e.Id == 1);
            Assert.Equal(2, item.QuestionCount);
            Assert.Equal("Zeta", item.ExamSeriesName);
            Assert.Equal("Published", item.Status);
        }
        if (page.Items.Any(e => e.Id == 3))
            Assert.Equal("Hidden", Assert.Single(page.Items, e => e.Id == 3).Status);
    }

    [Fact]
    public async Task List_PaginatesAndClampsNonPositivePageValues()
    {
        await using var context = await ContentTestContext.CreateAsync();
        var page = Assert.IsType<PagedResultDto<ExamListItemDto>>(
            (await context.Service.GetPagedExamsAsync(null, null, null, 2, 2)).Value);
        Assert.Equal(new[] { 2, 1 }, page.Items.Select(e => e.Id));
        Assert.Equal(4, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(2, page.Page);
        var clamped = Assert.IsType<PagedResultDto<ExamListItemDto>>(
            (await context.Service.GetPagedExamsAsync(null, null, null, 0, 0)).Value);
        Assert.Equal(1, clamped.Page);
        Assert.Equal(1, clamped.PageSize);
        Assert.Equal(4, Assert.Single(clamped.Items).Id);
    }

    [Fact]
    public async Task Detail_MapsNestedCmsContentAndAttemptCounts()
    {
        await using var context = await ContentTestContext.CreateAsync();
        var detail = Assert.IsType<ExamDetailDto>((await context.Service.GetExamByIdAsync(1)).Value);
        Assert.Equal("Published TOEIC", detail.Title);
        Assert.Equal("Zeta", detail.ExamSeriesName);
        Assert.Equal(15, detail.AttemptCount);
        Assert.Equal("exam.mp3", detail.AudioUrl);
        Assert.Equal(new[] { 2, 1 }, detail.Questions.Select(q => q.Id));
        Assert.Equal(new[] { 2, 1 }, detail.Passages.Select(p => p.Id));
        Assert.All(detail.Questions, q => Assert.Equal(detail.Title, q.ExamTitle));
        Assert.All(detail.Passages, p => Assert.Equal(detail.Title, p.ExamTitle));
        var question = Assert.Single(detail.Questions, q => q.Id == 1);
        Assert.Equal("B", question.CorrectOption);
        Assert.Equal("Because B", question.Explanation);
        Assert.Equal("question.mp3", question.AudioUrl);
        Assert.Equal(5, question.AudioStartSecond);
        Assert.Equal(10, question.AudioEndSecond);
        Assert.Equal(new[] { "A", "B" }, question.Options.Select(o => o.OptionLetter));
        var group = Assert.Single(detail.QuestionGroups);
        Assert.Equal("Emails", group.Name);
        Assert.Equal("group.png", group.ImageUrl);
        Assert.False((await context.Service.GetExamByIdAsync(999)).IsSuccess);
    }

    [Fact]
    public async Task Create_UsesDefaultsAndReturnsCreatedDtoThroughController()
    {
        await using var context = await ContentTestContext.CreateAsync();
        var controller = new ExamsController(context.Service);
        var response = Assert.IsType<CreatedAtActionResult>(await controller.CreateExam(
            new CreateExamDto("  New exam  ", "  ", " Description ", 90, 1, " audio.mp3 "), default));
        var detail = Assert.IsType<ExamDetailDto>(response.Value);
        Assert.Equal(nameof(ExamsController.GetExamById), response.ActionName);
        Assert.Equal(detail.Id, response.RouteValues!["id"]);
        Assert.Equal("New exam", detail.Title);
        Assert.Equal("Full Test", detail.ExamType);
        Assert.Equal("Description", detail.Description);
        Assert.Equal("audio.mp3", detail.AudioUrl);
        Assert.Equal(90, detail.Duration);
        Assert.Equal("Draft", detail.Status);
        Assert.Equal(0, detail.AttemptCount);
        Assert.False(detail.IsDeleted);
        context.Db.ChangeTracker.Clear();
        Assert.True(await context.Db.Exams.AnyAsync(e => e.Id == detail.Id));
    }

    [Theory]
    [InlineData(" ", 120, 1)]
    [InlineData("Valid", 0, 1)]
    [InlineData("Valid", 120, 999)]
    [InlineData("Valid", 120, 4)]
    public async Task Create_RejectsInvalidInputWithoutSaving(string title, int duration, int seriesId)
    {
        await using var context = await ContentTestContext.CreateAsync();
        var result = await context.Service.CreateExamAsync(new(title, null, null, duration, seriesId, null));
        Assert.False(result.IsSuccess);
        Assert.Equal(4, await context.Db.Exams.CountAsync());
    }

    [Fact]
    public async Task Update_PersistsMetadataAndPreservesTypeWhenBlank()
    {
        await using var context = await ContentTestContext.CreateAsync();
        var result = await context.Service.UpdateExamAsync(2,
            new(" Updated ", " ", " Description ", 60, 1, " audio.mp3 ", "published"));
        Assert.True(result.IsSuccess);
        context.Db.ChangeTracker.Clear();
        var exam = await context.Db.Exams.SingleAsync(e => e.Id == 2);
        Assert.Equal("Updated", exam.Title);
        Assert.Equal("Practice", exam.ExamType);
        Assert.Equal("Description", exam.Description);
        Assert.Equal("audio.mp3", exam.AudioUrl);
        Assert.Equal(60, exam.Duration);
        Assert.Equal(1, exam.ExamSeriesId);
        Assert.Equal(ExamStatus.Published, exam.Status);
        Assert.NotNull(exam.LastModifiedAt);
    }

    [Theory]
    [InlineData("", 120)]
    [InlineData("Valid", -1)]
    public async Task Update_RejectsInvalidMetadataWithoutSaving(string title, int duration)
    {
        await using var context = await ContentTestContext.CreateAsync();
        Assert.False((await context.Service.UpdateExamAsync(1, new(title, null, null, duration, 1, null, null))).IsSuccess);
        context.Db.ChangeTracker.Clear();
        Assert.Equal("Published TOEIC", (await context.Db.Exams.SingleAsync(e => e.Id == 1)).Title);
    }

    [Theory]
    [InlineData("Hidden", true)]
    [InlineData("Published", false)]
    [InlineData("Draft", false)]
    public async Task ChangeStatus_PersistsVisibilityAndDeletionTime(string status, bool deleted)
    {
        await using var context = await ContentTestContext.CreateAsync();
        Assert.True((await context.Service.ChangeStatusAsync(3, status)).IsSuccess);
        context.Db.ChangeTracker.Clear();
        var exam = await context.Db.Exams.SingleAsync(e => e.Id == 3);
        Assert.Equal(status, exam.Status.ToString());
        Assert.Equal(deleted, exam.IsDeleted);
        Assert.Equal(deleted, exam.DeletedAt.HasValue);
        Assert.NotNull(exam.LastModifiedAt);
    }

    [Fact]
    public async Task ChangeStatus_RejectsUnknownStatus()
    {
        await using var context = await ContentTestContext.CreateAsync();
        Assert.False((await context.Service.ChangeStatusAsync(1, "invalid")).IsSuccess);
        context.Db.ChangeTracker.Clear();
        Assert.Equal(ExamStatus.Published, (await context.Db.Exams.SingleAsync(e => e.Id == 1)).Status);
    }

    [Fact]
    public async Task DeleteWithAttempts_HidesContent_AndRestorePublishesIt()
    {
        await using var context = await ContentTestContext.CreateAsync();
        var result = Assert.IsType<DeleteExamResultDto>((await context.Service.DeleteExamAsync(1)).Value);
        Assert.True(result.SoftDeleted);
        context.Db.ChangeTracker.Clear();
        var hidden = await context.Db.Exams.SingleAsync(e => e.Id == 1);
        Assert.True(hidden.IsDeleted);
        Assert.Equal(ExamStatus.Hidden, hidden.Status);
        Assert.NotNull(hidden.DeletedAt);
        Assert.Equal(2, await context.Db.Questions.CountAsync());
        Assert.True((await context.Service.RestoreExamAsync(1)).IsSuccess);
        context.Db.ChangeTracker.Clear();
        var restored = await context.Db.Exams.SingleAsync(e => e.Id == 1);
        Assert.False(restored.IsDeleted);
        Assert.Null(restored.DeletedAt);
        Assert.Equal(ExamStatus.Published, restored.Status);
    }

    [Fact]
    public async Task DeleteWithoutAttempts_RemovesExamAndDependentContent()
    {
        await using var context = await ContentTestContext.CreateAsync();
        context.Attempts.Counts[1] = 0;
        var result = Assert.IsType<DeleteExamResultDto>((await context.Service.DeleteExamAsync(1)).Value);
        Assert.False(result.SoftDeleted);
        context.Db.ChangeTracker.Clear();
        Assert.False(await context.Db.Exams.AnyAsync(e => e.Id == 1));
        Assert.Empty(await context.Db.Questions.ToListAsync());
        Assert.Empty(await context.Db.QuestionOptions.ToListAsync());
        Assert.Empty(await context.Db.Passages.ToListAsync());
        Assert.Empty(await context.Db.QuestionGroups.ToListAsync());
        Assert.Equal(4, await context.Db.ExamSeries.CountAsync());
    }

    [Fact]
    public async Task MissingExam_FailsAllMutationOperations()
    {
        await using var context = await ContentTestContext.CreateAsync();
        Assert.False((await context.Service.UpdateExamAsync(999, new("Valid", null, null, 120, 1, null, null))).IsSuccess);
        Assert.False((await context.Service.ChangeStatusAsync(999, "Published")).IsSuccess);
        Assert.False((await context.Service.DeleteExamAsync(999)).IsSuccess);
        Assert.False((await context.Service.RestoreExamAsync(999)).IsSuccess);
    }

    [Fact]
    public async Task Series_ProjectsActiveRowsOrderedByNameThenDescendingYear()
    {
        await using var context = await ContentTestContext.CreateAsync();
        var series = Assert.IsType<List<ExamSeriesDto>>((await context.Service.GetExamSeriesAsync()).Value);
        Assert.Equal(new[] { 2, 3, 1 }, series.Select(s => s.Id));
        Assert.Equal(new[] { 2025, 2023, 2024 }, series.Select(s => s.Year));
    }
}
