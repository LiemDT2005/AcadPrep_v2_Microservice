using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.Infrastructure.Persistence;

public static class CatalogSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await db.Database.EnsureCreatedAsync();
        if (await db.Courses.AnyAsync()) return;

        var beginnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var midId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        db.Courses.AddRange(
            new Course
            {
                Id = beginnerId,
                Title = "TOEIC Starter 450+",
                Description = "Nền tảng Listening & Reading cho người mới bắt đầu.",
                Level = "Beginner",
                TargetScore = 450,
                Lessons =
                [
                    new Lesson { CourseId = beginnerId, Title = "Part 1 Photos", Skill = "Listening", Order = 1, DurationMinutes = 20,
                        Content = "Học cách mô tả ảnh: người, vật, hành động. Tập trung vào thì hiện tại tiếp diễn." },
                    new Lesson { CourseId = beginnerId, Title = "Part 5 Grammar Basics", Skill = "Grammar", Order = 2, DurationMinutes = 30,
                        Content = "Thì, giới từ, liên từ cơ bản thường gặp trong Part 5." },
                    new Lesson { CourseId = beginnerId, Title = "Vocabulary: Office", Skill = "Vocabulary", Order = 3, DurationMinutes = 25,
                        Content = "50 từ vựng văn phòng: meeting, deadline, invoice, schedule..." }
                ]
            },
            new Course
            {
                Id = midId,
                Title = "TOEIC Climb 650+",
                Description = "Chiến thuật Part 3-4 và Reading comprehension.",
                Level = "Intermediate",
                TargetScore = 650,
                Lessons =
                [
                    new Lesson { CourseId = midId, Title = "Part 3 Conversations", Skill = "Listening", Order = 1, DurationMinutes = 35,
                        Content = "Nghe hội thoại 3 người: đoán chủ đề, ai nói gì, chi tiết số liệu." },
                    new Lesson { CourseId = midId, Title = "Part 7 Single Passages", Skill = "Reading", Order = 2, DurationMinutes = 40,
                        Content = "Email, memo, advertisement — kỹ thuật skimming & scanning." }
                ]
            });

        await db.SaveChangesAsync();
    }
}
