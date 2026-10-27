using System.Text.Json;
using Content.Controllers;
using Content.DTOs;
using Content.Entities.Enums;
using Content.Repositories;
using Content.Services;
using Content.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Content.Tests;

public class LearnerExamServiceTests
{
    private static IExamService CreateService(ContentTestContext context) =>
        new ExamService(new ExamRepository(context.Db, ContentTestContext.MappingConfiguration.CreateMapper()),
            context.Attempts, ContentTestContext.MappingConfiguration.CreateMapper());

    [Fact]
    public async Task List_ProjectsMetadataAndCounts_AndFiltersBeforePagination()
    {
        await using var context = await ContentTestContext.CreateAsync();
        context.Db.ExamSeries.Add(new() { Id = 5, Name = "ETS", Year = 2026, CoverImageUrl = "cover.png" });
        context.Db.Exams.Add(new() { Id = 5, Title = "TOEIC New", Duration = 90, ExamSeriesId = 5,
            Status = ExamStatus.Published, CreatedAt = DateTime.UtcNow });
        await context.Db.SaveChangesAsync();
        var service = CreateService(context);

        var first = Assert.IsType<GetExamListResponse>((await service.GetExamListAsync(null, null, null, 1, 1)).Value);
        Assert.Equal(new[] { "All", "Alpha", "ETS", "Zeta" }, first.SeriesFilters);
        Assert.Equal(new[] { "All", "2026", "2025", "2024", "2023" }, first.YearFilters);
        Assert.Equal(2, first.Exams.TotalCount);
        Assert.Equal(2, first.Exams.TotalPage);
        Assert.Equal(5, Assert.Single(first.Exams.Items).Id);
        Assert.True(first.Exams.HasNextPage);

        var second = Assert.IsType<GetExamListResponse>((await service.GetExamListAsync(null, "All", null, 2, 1)).Value);
        var item = Assert.Single(second.Exams.Items);
        Assert.Equal(1, item.Id);
        Assert.Equal(2, item.QuestionCount);
        Assert.Equal(15, item.AttemptCount);
        Assert.True(second.Exams.HasPreviousPage);

        var filtered = Assert.IsType<GetExamListResponse>((await service.GetExamListAsync("  toeic  ", "ETS", 2026)).Value);
        var projected = Assert.Single(filtered.Exams.Items);
        Assert.Equal("ETS", projected.SeriesName);
        Assert.Equal(2026, projected.Year);
        Assert.Equal("cover.png", projected.CoverImageUrl);
        Assert.Equal(90, projected.Duration);
        Assert.Empty(Assert.IsType<GetExamListResponse>((await service.GetExamListAsync(null, "ETS", 2025)).Value).Exams.Items);
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(-1, -10, 1)]
    [InlineData(1, 1000, 1)]
    public async Task List_ClampsPagination(int pageIndex, int pageSize, int expectedPage)
    {
        await using var context = await ContentTestContext.CreateAsync();
        var page = Assert.IsType<GetExamListResponse>(
            (await CreateService(context).GetExamListAsync(null, null, null, pageIndex, pageSize)).Value).Exams;
        Assert.Equal(expectedPage, page.PageNumber);
        Assert.Single(page.Items);
        Assert.Equal(1, page.TotalPage);
    }

    [Fact]
    public async Task Detail_GroupsPartsAndTags_WithoutReturningAnswersOrLearnerHistory()
    {
        await using var context = await ContentTestContext.CreateAsync();
        context.Db.Questions.AddRange(
            new() { Id = 3, ExamId = 1, QuestionNumber = 149, Part = 7, TopicTag = "Emails" },
            new() { Id = 4, ExamId = 1, QuestionNumber = 150, Part = 7, TopicTag = "Emails" },
            new() { Id = 5, ExamId = 1, QuestionNumber = 151, Part = 7, TopicTag = "Ads" });
        await context.Db.SaveChangesAsync();
        var controller = new LearnerExamsController(CreateService(context));
        var response = Assert.IsType<OkObjectResult>(await controller.GetDetail(1));
        var detail = Assert.IsType<GetExamDetailDto>(response.Value);
        Assert.Equal("Published TOEIC", detail.Title);
        Assert.Equal("Zeta", detail.SeriesName);
        Assert.Equal(2024, detail.Year);
        Assert.Equal(120, detail.Duration);
        Assert.Equal(5, detail.TotalQuestions);
        Assert.Equal(15, detail.TotalAttempts);
        Assert.Equal(new[] { 6, 7 }, detail.Parts.Select(p => p.PartNumber));
        var partSeven = Assert.Single(detail.Parts, p => p.PartNumber == 7);
        Assert.Equal("Part 7: Reading Comprehension", partSeven.PartName);
        Assert.Equal(4, partSeven.QuestionCount);
        Assert.Equal(new[] { "Ads", "Emails" }, partSeven.Tags);
        var json = JsonSerializer.Serialize(detail).ToLowerInvariant();
        foreach (var field in new[] { "correctoption", "explanation", "questiontext", "attempthistory", "userid" })
            Assert.DoesNotContain(field, json);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(999)]
    public async Task Detail_RejectsMissingDraftHiddenAndDeletedExams(int id)
    {
        await using var context = await ContentTestContext.CreateAsync();
        var controller = new LearnerExamsController(CreateService(context));
        Assert.IsType<NotFoundObjectResult>(await controller.GetDetail(id));
    }

    [Fact]
    public async Task CmsChanges_AreImmediatelyVisibleToLearnerQueries()
    {
        await using var context = await ContentTestContext.CreateAsync();
        var service = CreateService(context);
        var created = Assert.IsType<ExamDetailDto>((await context.Service.CreateExamAsync(
            new("New CMS exam", null, null, 60, 1, null))).Value);
        Assert.False((await service.GetExamDetailAsync(created.Id)).IsSuccess);
        Assert.True((await context.Service.ChangeStatusAsync(created.Id, "Published")).IsSuccess);
        Assert.Equal("New CMS exam", Assert.IsType<GetExamDetailDto>((await service.GetExamDetailAsync(created.Id)).Value).Title);
        var page = Assert.IsType<GetExamListResponse>((await service.GetExamListAsync("New CMS", null, null)).Value);
        Assert.Equal(created.Id, Assert.Single(page.Exams.Items).Id);
        context.Attempts.Counts[created.Id] = 1;
        await context.Service.DeleteExamAsync(created.Id);
        Assert.False((await service.GetExamDetailAsync(created.Id)).IsSuccess);
        Assert.Empty(Assert.IsType<GetExamListResponse>((await service.GetExamListAsync("New CMS", null, null)).Value).Exams.Items);
        await context.Service.RestoreExamAsync(created.Id);
        Assert.True((await service.GetExamDetailAsync(created.Id)).IsSuccess);
    }
}
