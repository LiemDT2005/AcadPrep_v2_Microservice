using Learning.API.Data;
using Learning.API.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace Learning.API.Controllers;

/// <summary>
/// OData Controller Enterprise BI & Advanced Querying cho Learning Service:
/// Ho tro $filter, $orderby, $top, $skip, $select, $expand, $count truc tiep tren IQueryable.
/// </summary>
[Route("odata")]
[ApiController]
public class ODataLearningController(ILearningDbContext db) : ODataController
{
    [HttpGet("Vocabularies")]
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IQueryable<Vocabulary> GetVocabularies()
    {
        return db.Vocabularies.AsNoTracking();
    }

    [HttpGet("SavedVocabularies")]
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IQueryable<SavedVocabulary> GetSavedVocabularies()
    {
        return db.SavedVocabularies.AsNoTracking().Include(x => x.Vocabulary);
    }

    [HttpGet("ExamAttemptRecords")]
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IQueryable<ExamAttemptRecord> GetExamAttemptRecords()
    {
        return db.ExamAttemptRecords.AsNoTracking();
    }

    [HttpGet("Achievements")]
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IQueryable<Achievement> GetAchievements()
    {
        return db.Achievements.AsNoTracking();
    }
}
