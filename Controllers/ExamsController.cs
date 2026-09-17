using GenericTestWebApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController(TestPrepDbContext db) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() => Ok(await db.ExamCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id).ToListAsync());

    [HttpGet("{category}/tests")]
    public async Task<IActionResult> GetTests(string category)
    {
        var exam = await db.ExamCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Name == category && x.IsActive);
        if (exam is null) return NotFound(new { message = $"Exam category '{category}' was not found." });
        var tests = await db.MockTests.AsNoTracking().Where(x => x.ExamCategoryId == exam.Id && x.IsPublished).OrderBy(x => x.Id)
            .Select(x => new { x.Id, category = exam.Name, title = x.Title, questions = x.TotalQuestions, marks = x.TotalMarks, durationMinutes = x.DurationMinutes, x.NegativeMarking, x.Tag, x.IsFree }).ToListAsync();
        return Ok(tests);
    }
}