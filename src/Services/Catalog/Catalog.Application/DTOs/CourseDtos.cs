namespace Catalog.Application.DTOs;

public record CourseListItemDto(Guid Id, string Title, string Level, int TargetScore, int LessonCount, string Description);
public record LessonDto(Guid Id, string Title, string Skill, int Order, int DurationMinutes, string Content);
public record CourseDetailDto(Guid Id, string Title, string Description, string Level, int TargetScore, List<LessonDto> Lessons);
