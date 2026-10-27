using Microsoft.EntityFrameworkCore;

namespace Exam.Data;

public static class SessionStateSchema
{
    // Additive upgrade for the existing EnsureCreated databases. Does not drop learner data.
    public static async Task UpgradeAsync(ExamDbContext db)
    {
        if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'EXAM_SESSION_STATES', N'U') IS NULL
                BEGIN
                    CREATE TABLE [EXAM_SESSION_STATES] (
                        [Id] nvarchar(64) NOT NULL PRIMARY KEY, [Revision] nvarchar(32) NOT NULL, [UserId] nvarchar(450) NOT NULL,
                        [ExamId] int NOT NULL, [ExamTitle] nvarchar(max) NOT NULL,
                        [QuestionsJson] nvarchar(max) NOT NULL, [AnswersJson] nvarchar(max) NOT NULL,
                        [AudioJson] nvarchar(max) NOT NULL, [StartedAt] datetime2 NOT NULL,
                        [ExpiresAt] datetime2 NULL, [IsSubmitted] bit NOT NULL,
                        [CompletedAt] datetime2 NULL, [ListeningScore] int NOT NULL,
                        [ReadingScore] int NOT NULL, [SubmissionEventJson] nvarchar(max) NULL,
                        [EventPublished] bit NOT NULL
                    );
                    CREATE INDEX [IX_EXAM_SESSION_STATES_UserId_StartedAt] ON [EXAM_SESSION_STATES] ([UserId], [StartedAt]);
                END
                """);
        else
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "EXAM_SESSION_STATES" (
                    "Id" TEXT NOT NULL PRIMARY KEY, "Revision" TEXT NOT NULL, "UserId" TEXT NOT NULL,
                    "ExamId" INTEGER NOT NULL, "ExamTitle" TEXT NOT NULL,
                    "QuestionsJson" TEXT NOT NULL, "AnswersJson" TEXT NOT NULL, "AudioJson" TEXT NOT NULL,
                    "StartedAt" TEXT NOT NULL, "ExpiresAt" TEXT NULL, "IsSubmitted" INTEGER NOT NULL,
                    "CompletedAt" TEXT NULL, "ListeningScore" INTEGER NOT NULL, "ReadingScore" INTEGER NOT NULL,
                    "SubmissionEventJson" TEXT NULL, "EventPublished" INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_EXAM_SESSION_STATES_UserId_StartedAt" ON "EXAM_SESSION_STATES" ("UserId", "StartedAt");
                """);
    }
}
