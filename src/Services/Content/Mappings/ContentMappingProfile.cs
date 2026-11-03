using AutoMapper;
using Content.DTOs;
using Content.Entities;

namespace Content.Mappings;

public class ContentMappingProfile : Profile
{
    public ContentMappingProfile()
    {
        CreateMap<Exam, GetExamDto>()
            .ForMember(d => d.SeriesName, o => o.MapFrom(s => s.ExamSeries.Name))
            .ForMember(d => d.Year, o => o.MapFrom(s => s.ExamSeries.Year))
            .ForMember(d => d.CoverImageUrl, o => o.MapFrom(s => s.ExamSeries.CoverImageUrl))
            .ForMember(d => d.QuestionCount, o => o.MapFrom(s => s.Questions.Count))
            .ForMember(d => d.AttemptCount, o => o.MapFrom(s => 0));

        CreateMap<Exam, GetExamDetailDto>()
            .ForMember(d => d.SeriesName, o => o.MapFrom(s => s.ExamSeries.Name))
            .ForMember(d => d.Year, o => o.MapFrom(s => s.ExamSeries.Year))
            .ForMember(d => d.CoverImageUrl, o => o.MapFrom(s => s.ExamSeries.CoverImageUrl))
            .ForMember(d => d.TotalQuestions, o => o.Ignore())
            .ForMember(d => d.TotalAttempts, o => o.Ignore())
            .ForMember(d => d.Parts, o => o.Ignore());

        CreateMap<ExamSeries, ExamSeriesDto>();
        CreateMap<QuestionOption, QuestionOptionDto>();
        CreateMap<Passage, PassageDetailDto>();
        CreateMap<Question, QuestionDetailDto>()
            .ForCtorParam(nameof(QuestionDetailDto.Options), o => o.MapFrom(s => s.QuestionOptions));
        CreateMap<QuestionGroup, QuestionGroupDetailDto>();

        // Attempt counts are supplied by IExamAttemptChecker after mapping.
        CreateMap<Exam, ExamListItemDto>()
            .ForCtorParam(nameof(ExamListItemDto.Status), o => o.MapFrom(s => s.IsDeleted ? "Hidden" : s.Status.ToString()))
            .ForCtorParam(nameof(ExamListItemDto.AttemptCount), o => o.MapFrom(s => 0))
            .ForCtorParam(nameof(ExamListItemDto.QuestionCount), o => o.MapFrom(s => s.Questions.Count));

        CreateMap<Exam, ExamDetailDto>()
            .ForCtorParam(nameof(ExamDetailDto.Status), o => o.MapFrom(s => s.IsDeleted ? "Hidden" : s.Status.ToString()))
            .ForCtorParam(nameof(ExamDetailDto.AttemptCount), o => o.MapFrom(s => 0))
            .ForCtorParam(nameof(ExamDetailDto.Questions), o => o.MapFrom(s => s.Questions.OrderBy(q => q.QuestionNumber)))
            .ForCtorParam(nameof(ExamDetailDto.Passages), o => o.MapFrom(s => s.Passages.OrderBy(p => p.DisplayOrder)));
    }
}
