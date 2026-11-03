using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Common.Events;
using BuildingBlocks.Common.Results;
using Exam.Common;
using Exam.DTOs;
using Exam.Entities;
using Exam.Repositories.Interfaces;
using Exam.Services.Interfaces;

namespace Exam.Services;

public class ExamLifecycleService(ISessionStateRepository states, IExamSessionService sessions,
    IQuestionRepository questions, IExamPaperSource paperSource, IExamQuotaPolicy quota) : IExamLifecycleService
{
    public async Task<Result<int>> StartAsync(int examId, string userId, StartPracticeRequest? practice, CancellationToken ct)
    {
        if (practice is not null && (practice.SelectedPartNumbers is not { Count: > 0 }
            || practice.SelectedPartNumbers.Any(p => p < 1 || p > 7)
            || practice.TimeLimitMinutes is <= 0 or > 120 || practice.SelectedTags is null
            || practice.SelectedTags.Any(string.IsNullOrWhiteSpace)
            || string.Join(',', practice.SelectedTags).Length > 500))
            return Result<int>.Failure("Invalid practice selection. Choose Parts 1–7 and a time limit of 1–120 minutes or no limit.");
        ExamPaperSnapshot? source;
        try { source = await paperSource.GetAsync(examId, ct); }
        catch (HttpRequestException) { return Result<int>.Failure("Content service is unavailable. Please retry."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return Result<int>.Failure("Content service timed out. Please retry."); }
        if (source is null) return Result<int>.Failure("Exam not found or is not published.");
        if (source.Duration <= 0 || source.Duration > 1440) return Result<int>.Failure("Invalid exam duration.");
        var paper = source.Questions;
        if (practice is not null)
        {
            var tags = practice.SelectedTags.Select(t => t.Trim()).ToHashSet();
            // Include whole listening/reading groups when one of their questions matches a tag.
            string Unit(ReviewQuestionDto q) => q.Part is 3 or 4 or 7 && q.QuestionGroupId.HasValue
                ? $"{q.Part}:g:{q.QuestionGroupId}" : q.Part == 6 && q.PassageId.HasValue ? $"6:p:{q.PassageId}" : $"q:{q.QuestionId}";
            paper = paper.Where(q => practice.SelectedPartNumbers.Contains(q.Part)).GroupBy(Unit)
                .Where(g => tags.Count == 0 || g.Any(q => q.TopicTag is not null && tags.Contains(q.TopicTag)))
                .OrderBy(g => g.First().Part).ThenBy(g => g.Min(q => q.QuestionNumber))
                .SelectMany(g => g.OrderBy(q => q.QuestionNumber)).ToList();
        }
        if (paper.Count == 0) return Result<int>.Failure("No questions match your selection.");
        if (paper.Any(q => !q.Options.Any(o => o.Letter == q.CorrectOption)))
            return Result<int>.Failure("The published paper contains an invalid answer key. Contact the content moderator.");
        var allowed = await quota.CanStartAsync(userId, practice is not null, ct);
        if (!allowed.IsSuccess || !allowed.Value) return Result<int>.Failure(allowed.Error ?? "Your plan does not allow another session.");
        var now = DateTime.UtcNow;
        var minutes = practice is null ? source.Duration : practice.TimeLimitMinutes;
        var state = new ExamSessionState
        {
            UserId = userId, ExamId = source.Id, ExamTitle = source.Title, StartedAt = now,
            ExpiresAt = minutes.HasValue ? now.AddMinutes(minutes.Value) : null,
            QuestionsJson = JsonSerializer.Serialize(paper)
        };
        var id = await states.CreateAsync(state, practice is not null, practice?.TimeLimitMinutes,
            practice is null ? "1,2,3,4,5,6,7" : string.Join(',', practice.SelectedPartNumbers.Distinct().Order()), ct);
        return Result<int>.Success(id);
    }

    public async Task CaptureAsync(int id, string mode, string userId, CancellationToken ct) =>
        _ = await LoadAsync(id, mode, userId, ct);

    private async Task<ExamSessionState?> LoadAsync(int id, string mode, string userId, CancellationToken ct)
    {
        if (mode is not ("full-test" or "practice")) return null;
        var state = await states.GetAsync($"{mode}:{id}", userId, ct);
        if (state is not null) return state;
        var session = mode == "practice" ? await sessions.GetPracticeAsync(id, userId, ct)
            : await sessions.GetFullTestAsync(id, userId, ct);
        if (session is null) return null;
        var entities = await questions.GetSessionQuestionsAsync(session.ExamId, session.Questions.Select(q => q.Id).ToList(), ct);
        var byId = entities.ToDictionary(q => q.Id);
        var review = session.Questions.Select(q => ToReviewQuestion(byId[q.Id])).ToList();
        state = new ExamSessionState
        {
            Id = $"{mode}:{id}", UserId = userId, ExamId = session.ExamId, ExamTitle = session.ExamTitle,
            StartedAt = session.StartedAt, ExpiresAt = session.ExpiresAt,
            QuestionsJson = JsonSerializer.Serialize(review)
        };
        await states.SaveAsync(state, ct);
        return state;
    }

    private static ReviewQuestionDto ToReviewQuestion(Question q) => new()
    {
        QuestionId = q.Id, QuestionNumber = q.QuestionNumber, Part = q.Part, QuestionText = q.QuestionText,
        ImageUrl = q.ImageUrl ?? q.QuestionGroup?.ImageUrl,
        AudioUrl = q.Part <= 4 ? q.AudioUrl ?? q.QuestionGroup?.AudioUrl ?? q.Exam?.AudioUrl : null,
        AudioStartSecond = q.AudioStartSecond ?? q.QuestionGroup?.AudioStartSecond,
        AudioEndSecond = q.AudioEndSecond ?? q.QuestionGroup?.AudioEndSecond,
        QuestionGroupId = q.QuestionGroupId, PassageId = q.PassageId,
        Passages = (q.QuestionGroup?.Passages.Count > 0 ? q.QuestionGroup.Passages
            : q.Passage is not null ? new[] { q.Passage }.AsEnumerable() : [])
            .OrderBy(p => p.DisplayOrder).Select(p => new SessionPassageDto
            { Id = p.Id, DisplayOrder = p.DisplayOrder, Content = p.Content, ImageUrl = p.ImageUrl }).ToList(),
        PassageContent = q.Passage?.Content ?? string.Join("\n\n", q.QuestionGroup?.Passages.OrderBy(p => p.DisplayOrder).Select(p => p.Content) ?? []),
        PassageImageUrl = q.Passage?.ImageUrl,
        TopicTag = q.TopicTag, CorrectOption = q.CorrectOption.ToString(),
        Explanation = q.Explanation ?? q.QuestionGroup?.Explanation ?? q.Passage?.Explanation,
        Options = q.QuestionOptions.OrderBy(o => o.OptionLetter)
            .Select(o => new ReviewOptionDto { Letter = o.OptionLetter.ToString(), Text = o.OptionText }).ToList()
    };

    private static List<ReviewQuestionDto> Paper(ExamSessionState s) =>
        JsonSerializer.Deserialize<List<ReviewQuestionDto>>(s.QuestionsJson)!;
    private static Dictionary<int, string> Answers(ExamSessionState s) =>
        JsonSerializer.Deserialize<Dictionary<int, string>>(s.AnswersJson)!;

    public async Task<ExamSessionDto?> GetAsync(int id, string mode, string userId, CancellationToken ct)
    {
        var state = await LoadAsync(id, mode, userId, ct);
        if (state is null) return null;
        return new ExamSessionDto
        {
            SessionId = id, ExamId = state.ExamId, ExamTitle = state.ExamTitle, Mode = mode,
            StartedAt = DateTime.SpecifyKind(state.StartedAt, DateTimeKind.Utc),
            ExpiresAt = state.ExpiresAt.HasValue ? DateTime.SpecifyKind(state.ExpiresAt.Value, DateTimeKind.Utc) : null,
            Answers = Answers(state), IsSubmitted = state.IsSubmitted,
            AudioProgress = JsonSerializer.Deserialize<Dictionary<string, AudioCheckpointDto>>(state.AudioJson)!,
            Questions = Paper(state).Select(q => new SessionQuestionDto
            {
                Id = q.QuestionId, QuestionNumber = q.QuestionNumber, Part = q.Part, QuestionText = q.QuestionText,
                ImageUrl = q.ImageUrl, QuestionGroupId = q.QuestionGroupId, PassageId = q.PassageId,
                AudioUrl = q.AudioUrl, AudioStartSecond = q.AudioStartSecond, AudioEndSecond = q.AudioEndSecond,
                Passages = q.Passages, Options = q.Options.Select(o => new SessionOptionDto(o.Letter, o.Text)).ToList()
            }).ToList()
        };
    }

    private static string? Apply(ExamSessionState state, SaveSessionRequest request)
    {
        if (request.Answers is null) return "Answers are required.";
        var paper = Paper(state).ToDictionary(q => q.QuestionId);
        foreach (var (id, value) in request.Answers)
        {
            if (!paper.TryGetValue(id, out var q)) return "Question does not belong to this session.";
            if (value is not null && !q.Options.Any(o => o.Letter == value)) return "Invalid answer option.";
        }
        if (request.AudioProgress is not null)
        {
            var validKeys = paper.Values.Select(q => q.Part is 3 or 4 or 7 && q.QuestionGroupId.HasValue
                ? $"g-{q.QuestionGroupId}" : q.Part == 6 && q.PassageId.HasValue ? $"p-{q.PassageId}" : $"q-{q.QuestionId}").ToHashSet();
            if (request.AudioProgress.Any(kv => !validKeys.Contains(kv.Key) || kv.Value is null
                || !double.IsFinite(kv.Value.Position) || kv.Value.Position < 0 || kv.Value.Position > 86400))
                return "Invalid audio checkpoint.";
        }
        var answers = Answers(state);
        foreach (var (id, value) in request.Answers)
            if (value is null) answers.Remove(id); else answers[id] = value;
        state.AnswersJson = JsonSerializer.Serialize(answers);
        if (request.AudioProgress is not null)
        {
            var audio = JsonSerializer.Deserialize<Dictionary<string, AudioCheckpointDto>>(state.AudioJson)!;
            foreach (var (key, value) in request.AudioProgress)
            {
                if (key.Length > 100 || !double.IsFinite(value.Position) || value.Position < 0) continue;
                // Full-test playback cannot be rewound by a later checkpoint.
                if (state.Id.StartsWith("full-test:") && audio.TryGetValue(key, out var previous))
                    audio[key] = new(Math.Max(previous.Position, value.Position), previous.Completed || value.Completed);
                else audio[key] = value;
            }
            state.AudioJson = JsonSerializer.Serialize(audio);
        }
        return null;
    }

    public async Task<Result<bool>> SaveAsync(int id, string mode, string userId, SaveSessionRequest request, CancellationToken ct)
    {
        var state = await LoadAsync(id, mode, userId, ct);
        if (state is null) return Result<bool>.Failure("Session not found.");
        if (state.IsSubmitted) return Result<bool>.Failure("Session has already been submitted.");
        if (state.ExpiresAt <= DateTime.UtcNow) return Result<bool>.Failure("Time has expired. Submit the saved answers.");
        var error = Apply(state, request);
        if (error is not null) return Result<bool>.Failure(error);
        await states.SaveAsync(state, ct);
        return Result<bool>.Success(true);
    }

    public async Task<Result<AttemptReviewDto>> SubmitAsync(int id, string mode, string userId, SaveSessionRequest request, CancellationToken ct)
    {
        var state = await LoadAsync(id, mode, userId, ct);
        if (state is null) return Result<AttemptReviewDto>.Failure("Session not found.");
        if (state.IsSubmitted) return Result<AttemptReviewDto>.Success(ToReview(state));
        // The server clock is authoritative. A late submit grades only answers saved before expiry.
        if (!state.ExpiresAt.HasValue || state.ExpiresAt > DateTime.UtcNow)
        {
            var error = Apply(state, request);
            if (error is not null) return Result<AttemptReviewDto>.Failure(error);
        }
        state.IsSubmitted = true;
        state.CompletedAt = DateTime.UtcNow;
        var paper = Paper(state);
        var answers = Answers(state);
        int Correct(int min, int max) => paper.Count(q => q.Part >= min && q.Part <= max
            && answers.GetValueOrDefault(q.QuestionId) == q.CorrectOption);
        var listening = Correct(1, 4);
        var reading = Correct(5, 7);
        var practice = mode == "practice";
        state.ListeningScore = practice ? listening : ToeicScoreConverter.CalculateListeningScore(listening);
        state.ReadingScore = practice ? reading : ToeicScoreConverter.CalculateReadingScore(reading);
        // Practice uses correct-count scoring; only full tests feed TOEIC score analytics.
        if (!practice && !userId.StartsWith("guest:"))
        {
            var breakdown = paper.GroupBy(q => q.Part).ToDictionary(g => g.Key,
                g => (int)Math.Round(g.Count(q => answers.GetValueOrDefault(q.QuestionId) == q.CorrectOption) * 100.0 / g.Count()));
            var evt = new TestSubmittedEvent(Guid.NewGuid(), userId, StableId($"exam:{state.ExamId}"), state.ExamTitle,
                state.ListeningScore + state.ReadingScore, state.ListeningScore, state.ReadingScore,
                paper.Count, listening + reading, breakdown);
            state.SubmissionEventJson = JsonSerializer.Serialize(evt);
        }
        await states.CompleteAsync(state, listening, reading, paper.Count(q => q.Part <= 4), paper.Count(q => q.Part >= 5), ct);
        return Result<AttemptReviewDto>.Success(ToReview(state));
    }

    public static Guid StableId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"AcadPrep:{key}"))[..16]);

    private static AttemptReviewDto ToReview(ExamSessionState state)
    {
        var paper = Paper(state);
        var answers = Answers(state);
        foreach (var q in paper)
        {
            q.SelectedOption = answers.GetValueOrDefault(q.QuestionId);
            q.IsAnswered = q.SelectedOption is not null;
            q.IsCorrect = q.SelectedOption == q.CorrectOption;
        }
        return new AttemptReviewDto
        {
            AttemptOrSessionId = int.Parse(state.Id.Split(':')[1]), IsPractice = state.Id.StartsWith("practice:"),
            ExamId = state.ExamId, ExamTitle = state.ExamTitle, Questions = paper,
            ListeningScore = state.ListeningScore, ReadingScore = state.ReadingScore,
            TotalScore = state.ListeningScore + state.ReadingScore,
            MaxScore = state.Id.StartsWith("practice:") ? paper.Count : 990,
            CorrectCount = paper.Count(q => q.IsCorrect), IncorrectCount = paper.Count(q => q.IsAnswered && !q.IsCorrect),
            UnansweredCount = paper.Count(q => !q.IsAnswered), CompletedAt = state.CompletedAt
        };
    }

    public async Task<AttemptReviewDto?> ReviewAsync(int id, string mode, string userId, CancellationToken ct)
    {
        var state = await states.GetAsync($"{mode}:{id}", userId, ct);
        return state is { IsSubmitted: true } ? ToReview(state) : null;
    }

    public async Task<StudyHistoryResultDto> HistoryAsync(string userId, CancellationToken ct) => new()
    {
        Items = (await states.GetHistoryAsync(userId, ct)).Select(s => new StudyHistoryItemDto
        {
            ActivityType = s.Id.StartsWith("practice:") ? "practice" : "exam", Title = s.ExamTitle,
            Description = s.IsSubmitted ? $"Completed — {s.ListeningScore + s.ReadingScore}/{(s.Id.StartsWith("practice:") ? Paper(s).Count : 990)}"
                : "In progress — resume your saved session",
            OccurredAt = s.CompletedAt ?? s.StartedAt,
            LinkUrl = s.IsSubmitted ? $"/Exams/Review?{(s.Id.StartsWith("practice:") ? "sessionId" : "attemptId")}={s.Id.Split(':')[1]}"
                : $"/Exams/Take/{s.Id.Split(':')[1]}?mode={s.Id.Split(':')[0]}",
            Icon = s.Id.StartsWith("practice:") ? "fitness_center" : "quiz", ColorType = "primary"
        }).OrderByDescending(x => x.OccurredAt).ToList()
    };

    public async Task<ExamAttemptsResultDto> AttemptsAsync(string userId, CancellationToken ct) => new()
    {
        Attempts = (await states.GetHistoryAsync(userId, ct)).Where(s => s.IsSubmitted && s.Id.StartsWith("full-test:"))
            .Select(s => new ExamAttemptListItemDto
            {
                AttemptId = int.Parse(s.Id.Split(':')[1]), ExamId = s.ExamId, ExamTitle = s.ExamTitle,
                ListeningScore = s.ListeningScore, ReadingScore = s.ReadingScore, TotalScore = s.ListeningScore + s.ReadingScore,
                StartedAt = s.StartedAt, CompletedAt = s.CompletedAt
            }).OrderByDescending(a => a.CompletedAt).ToList()
    };

    public async Task<IncorrectAnswersResultDto> IncorrectAsync(string userId, CancellationToken ct)
    {
        var latest = (await states.GetHistoryAsync(userId, ct)).Where(s => s.IsSubmitted)
            .OrderByDescending(s => s.CompletedAt).SelectMany(s => ToReview(s).Questions.Where(q => !q.IsCorrect)
            .Select(q => new IncorrectAnswerItemDto
            {
                QuestionId = q.QuestionId, QuestionNumber = q.QuestionNumber, Part = q.Part, ExamId = s.ExamId,
                ExamTitle = s.ExamTitle, AttemptId = int.Parse(s.Id.Split(':')[1]), IsPractice = s.Id.StartsWith("practice:"),
                QuestionText = q.QuestionText, TopicTag = q.TopicTag, SelectedOption = q.SelectedOption,
                CorrectOption = q.CorrectOption, Explanation = q.Explanation, AttemptedAt = s.CompletedAt,
                Options = q.Options.Select(o => new IncorrectOptionDto { Letter = o.Letter, Text = o.Text }).ToList()
            })).DistinctBy(q => (q.ExamId, q.QuestionId)).ToList();
        return new IncorrectAnswersResultDto
        {
            TotalIncorrect = latest.Count, Groups = latest.GroupBy(q => q.Part).OrderBy(g => g.Key)
                .Select(g => new IncorrectAnswerGroupDto { GroupKey = $"part-{g.Key}", GroupLabel = $"Part {g.Key}", Items = g.ToList() }).ToList()
        };
    }
}
