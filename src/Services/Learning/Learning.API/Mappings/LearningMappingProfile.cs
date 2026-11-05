using AutoMapper;
using Learning.API.DTOs;
using Learning.API.Entities;

namespace Learning.API.Mappings;

public class LearningMappingProfile : Profile
{
    public LearningMappingProfile()
    {
        // Vocabulary -> VocabularyDetailDto
        CreateMap<Vocabulary, VocabularyDetailDto>()
            .ConstructUsing(src => new VocabularyDetailDto(
                src.Id,
                src.Word,
                src.Phonetic,
                src.Meaning,
                src.Example
            ));

        // VocabPassage -> VocabPassageDto
        CreateMap<VocabPassage, VocabPassageDto>()
            .ConstructUsing(src => new VocabPassageDto(
                src.Id,
                src.Content,
                src.VocabularyId
            ));

        // Vocabulary -> VocabContextDto
        CreateMap<Vocabulary, VocabContextDto>()
            .ConstructUsing((src, ctx) => new VocabContextDto(
                src.Id,
                src.Word,
                src.Phonetic,
                src.Meaning,
                src.Example,
                ctx.Mapper.Map<List<VocabPassageDto>>(src.Passages)
            ));

        // SavedVocabulary -> SavedVocabularyDto
        CreateMap<SavedVocabulary, SavedVocabularyDto>()
            .ConstructUsing(src => new SavedVocabularyDto(
                src.VocabularyId,
                src.Vocabulary != null ? src.Vocabulary.Word : string.Empty,
                src.Vocabulary != null ? src.Vocabulary.Phonetic : null,
                src.Vocabulary != null ? src.Vocabulary.Meaning : string.Empty,
                src.Vocabulary != null ? src.Vocabulary.Example : null,
                src.Interval,
                src.NextReviewDate,
                src.DateSaved
            ));

        // Achievement -> AchievementDto
        CreateMap<Achievement, AchievementDto>()
            .ConstructUsing(src => new AchievementDto(
                src.Id,
                src.Name,
                src.Description,
                src.IconUrl,
                src.ConditionType,
                src.ConditionValue,
                false,
                null
            ));

        // CreateAchievementRequest / UpdateAchievementRequest -> Achievement
        CreateMap<CreateAchievementRequest, Achievement>();
        CreateMap<UpdateAchievementRequest, Achievement>();

        // ExamAttemptRecord -> ScoreProgressItemDto
        CreateMap<ExamAttemptRecord, ScoreProgressItemDto>()
            .ConstructUsing(src => new ScoreProgressItemDto(
                src.SubmittedAt,
                src.Score,
                src.ExamTitle
            ));
    }
}
