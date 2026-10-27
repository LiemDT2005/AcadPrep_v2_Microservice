using AutoMapper;
using Exam.Data;
using Exam.Mappings;
using Exam.Repositories;
using Exam.Services;
using Exam.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exam.Tests;

internal static class TestServices
{
    internal static readonly MapperConfiguration MappingConfiguration =
        new(cfg => cfg.AddProfile<ExamMappingProfile>(), NullLoggerFactory.Instance);

    internal static IMapper CreateMapper() => MappingConfiguration.CreateMapper();

    internal static IExamSessionService CreateSessionService(IExamDbContext db) =>
        new ExamSessionService(new ExamRepository(db), new QuestionRepository(db),
            new ExamAttemptRepository(db), new PracticeSessionRepository(db), CreateMapper());

    internal static IExamAttemptService CreateAttemptService(IExamDbContext db) =>
        new ExamAttemptService(new ExamAttemptRepository(db), CreateMapper());
}
