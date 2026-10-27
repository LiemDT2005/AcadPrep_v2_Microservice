using AutoMapper;
using Exam.DTOs;
using Exam.Entities;

namespace Exam.Mappings;

public class ExamMappingProfile : Profile
{
    public ExamMappingProfile()
    {
        CreateMap<ExamAttempt, UserAttemptHistoryDto>()
            .ForMember(d => d.AttemptId, o => o.MapFrom(s => s.Id));

        CreateMap<Passage, SessionPassageDto>();
        CreateMap<QuestionOption, SessionOptionDto>()
            .ForCtorParam("Letter", o => o.MapFrom(s => s.OptionLetter.ToString()))
            .ForCtorParam("Text", o => o.MapFrom(s => s.OptionText));

        CreateMap<Question, SessionQuestionDto>()
            .ForMember(d => d.GroupImageUrl, o => o.MapFrom(s => s.QuestionGroup == null ? null : s.QuestionGroup.ImageUrl))
            .ForMember(d => d.Passages, o => o.MapFrom(s => GetPassages(s)))
            .ForMember(d => d.Options, o => o.MapFrom(s => s.QuestionOptions.OrderBy(o => o.OptionLetter)));
    }

    private static IEnumerable<Passage> GetPassages(Question question)
    {
        IEnumerable<Passage> passages = question.QuestionGroup?.Passages.Count > 0
            ? question.QuestionGroup.Passages
            : question.Passage is not null ? new[] { question.Passage } : Array.Empty<Passage>();
        return passages.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id);
    }
}
