using System.Text;
using Learning.API.DTOs;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Learning.API.Formatters;

/// <summary>
/// Custom Output Formatter chuan RESTful Content Negotiation (Accept: text/csv):
/// Tu dong chuyen doi du lieu Bao cao Admin (ProgressReportDto, ExamStatsItemDto),
/// So tay tu vung (SavedVocabularyDto) va Bang xep hang (LeaderboardResultDto) sang dinh dang CSV (ho tro UTF-8 BOM cho Excel).
/// </summary>
public class CsvOutputFormatter : TextOutputFormatter
{
    public CsvOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("text/csv"));
        SupportedEncodings.Add(Encoding.UTF8);
        SupportedEncodings.Add(Encoding.Unicode);
    }

    protected override bool CanWriteType(Type? type)
    {
        if (type is null) return false;

        return typeof(ProgressReportDto).IsAssignableFrom(type)
            || typeof(PaginatedListDto<ExamStatsItemDto>).IsAssignableFrom(type)
            || typeof(IEnumerable<ExamStatsItemDto>).IsAssignableFrom(type)
            || typeof(PaginatedListDto<SavedVocabularyDto>).IsAssignableFrom(type)
            || typeof(IEnumerable<SavedVocabularyDto>).IsAssignableFrom(type)
            || typeof(LeaderboardResultDto).IsAssignableFrom(type)
            || typeof(IEnumerable<LeaderboardEntryDto>).IsAssignableFrom(type);
    }

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        var httpContext = context.HttpContext;
        var sb = new StringBuilder();

        // Add UTF-8 BOM so Microsoft Excel displays Vietnamese diacritics properly
        sb.Append('\uFEFF');

        switch (context.Object)
        {
            case ProgressReportDto report:
                WriteProgressReportCsv(sb, report);
                break;

            case PaginatedListDto<ExamStatsItemDto> pagedExams:
                WriteExamStatsCsv(sb, pagedExams.Items);
                break;

            case IEnumerable<ExamStatsItemDto> examStats:
                WriteExamStatsCsv(sb, examStats);
                break;

            case PaginatedListDto<SavedVocabularyDto> pagedVocab:
                WriteSavedVocabularyCsv(sb, pagedVocab.Items);
                break;

            case IEnumerable<SavedVocabularyDto> vocabList:
                WriteSavedVocabularyCsv(sb, vocabList);
                break;

            case LeaderboardResultDto leaderboard:
                WriteLeaderboardCsv(sb, leaderboard.Entries);
                break;

            case IEnumerable<LeaderboardEntryDto> entries:
                WriteLeaderboardCsv(sb, entries);
                break;
        }

        await httpContext.Response.WriteAsync(sb.ToString(), selectedEncoding);
    }

    private static void WriteProgressReportCsv(StringBuilder sb, ProgressReportDto report)
    {
        sb.AppendLine("Date,CompletedExamsCount,AverageScore");
        foreach (var item in report.DataPoints)
        {
            sb.AppendLine($"{item.Date:yyyy-MM-dd},{item.CompletedExamsCount},{item.AverageScore:F1}");
        }
    }

    private static void WriteExamStatsCsv(StringBuilder sb, IEnumerable<ExamStatsItemDto> items)
    {
        sb.AppendLine("ExamId,ExamName,TotalAttempts,AverageScore,HighestScore,LowestScore,CompletionRatePercent");
        foreach (var item in items)
        {
            sb.AppendLine($"{item.ExamId},{EscapeCsv(item.ExamName)},{item.TotalAttempts},{item.AverageScore:F1},{item.HighestScore},{item.LowestScore},{item.CompletionRate:F1}");
        }
    }

    private static void WriteSavedVocabularyCsv(StringBuilder sb, IEnumerable<SavedVocabularyDto> items)
    {
        sb.AppendLine("VocabularyId,Word,Phonetic,Meaning,Example,IntervalDays,NextReviewDate,DateSaved");
        foreach (var item in items)
        {
            sb.AppendLine($"{item.VocabularyId},{EscapeCsv(item.Word)},{EscapeCsv(item.Phonetic)},{EscapeCsv(item.Meaning)},{EscapeCsv(item.Example)},{item.Interval},{item.NextReviewDate:yyyy-MM-dd},{item.DateSaved:yyyy-MM-dd HH:mm:ss}");
        }
    }

    private static void WriteLeaderboardCsv(StringBuilder sb, IEnumerable<LeaderboardEntryDto> items)
    {
        sb.AppendLine("Rank,UserId,Name,TotalScore,ExamsDone,StreakDays");
        foreach (var item in items)
        {
            sb.AppendLine($"{item.Rank},{EscapeCsv(item.UserId)},{EscapeCsv(item.Name)},{item.TotalScore},{item.ExamsDone},{item.StreakDays}");
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
