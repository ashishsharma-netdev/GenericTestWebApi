using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController(TestPrepDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats() => Ok(new { exams = await db.ExamCategories.CountAsync(), tests = await db.MockTests.CountAsync(), questions = await db.TestQuestions.CountAsync(), publishedTests = await db.MockTests.CountAsync(x => x.IsPublished) });

    [HttpGet("categories")]
    public async Task<IActionResult> Categories() => Ok(await db.ExamCategories.OrderBy(x => x.Id).ToListAsync());

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(CategoryRequest request)
    {
        if (await db.ExamCategories.AnyAsync(x => x.Name == request.Name)) return Conflict(new { message = "Category already exists." });
        var item = new ExamCategoryEntity { Name=request.Name.Trim(), Description=request.Description.Trim(), Icon=request.Icon ?? "◉", Accent=request.Accent ?? "#1264d8" };
        db.ExamCategories.Add(item); await db.SaveChangesAsync(); return Created($"api/admin/categories/{item.Id}", item);
    }

    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, CategoryRequest request)
    {
        var item = await db.ExamCategories.FindAsync(id); if (item is null) return NotFound();
        item.Name=request.Name.Trim(); item.Description=request.Description.Trim(); item.Icon=request.Icon ?? item.Icon; item.Accent=request.Accent ?? item.Accent; item.IsActive=request.IsActive;
        await db.SaveChangesAsync(); return Ok(item);
    }

    [HttpGet("tests")]
    public async Task<IActionResult> Tests() => Ok(await db.MockTests.Include(x => x.ExamCategory).OrderByDescending(x => x.Id).Select(x => new { x.Id, categoryId=x.ExamCategoryId, category=x.ExamCategory.Name, x.Title, questions=x.TotalQuestions, marks=x.TotalMarks, x.DurationMinutes, x.NegativeMarking, x.Tag, x.IsFree, x.IsPublished }).ToListAsync());

    [HttpPost("tests")]
    public async Task<IActionResult> CreateTest(TestRequest request)
    {
        if (!await db.ExamCategories.AnyAsync(x => x.Id == request.CategoryId)) return BadRequest(new { message="Invalid exam category." });
        var item = new MockTestEntity { ExamCategoryId=request.CategoryId, Title=request.Title.Trim(), TotalQuestions=request.TotalQuestions, TotalMarks=request.TotalMarks, DurationMinutes=request.DurationMinutes, NegativeMarking=request.NegativeMarking, Tag=request.Tag ?? "", IsFree=request.IsFree, IsPublished=request.IsPublished };
        db.MockTests.Add(item); await db.SaveChangesAsync(); return Created($"api/admin/tests/{item.Id}", item);
    }

    [HttpPut("tests/{id:int}")]
    public async Task<IActionResult> UpdateTest(int id, TestRequest request)
    {
        var item = await db.MockTests.FindAsync(id); if (item is null) return NotFound();
        item.ExamCategoryId=request.CategoryId; item.Title=request.Title.Trim(); item.TotalQuestions=request.TotalQuestions; item.TotalMarks=request.TotalMarks; item.DurationMinutes=request.DurationMinutes; item.NegativeMarking=request.NegativeMarking; item.Tag=request.Tag ?? ""; item.IsFree=request.IsFree; item.IsPublished=request.IsPublished;
        await db.SaveChangesAsync(); return Ok(item);
    }

    [HttpDelete("tests/{id:int}")]
    public async Task<IActionResult> DeleteTest(int id) { var item=await db.MockTests.FindAsync(id); if(item is null)return NotFound(); db.MockTests.Remove(item); await db.SaveChangesAsync(); return NoContent(); }

    [HttpGet("tests/{testId:int}/questions")]
    public async Task<IActionResult> Questions(int testId) => Ok(await db.TestQuestions.Where(x => x.MockTestId == testId).OrderBy(x => x.DisplayOrder).ToListAsync());

    [HttpPost("tests/{testId:int}/questions")]
    public async Task<IActionResult> CreateQuestion(int testId, QuestionRequest request)
    {
        if (!await db.MockTests.AnyAsync(x => x.Id == testId)) return NotFound(new { message="Test not found." });
        var item = new TestQuestionEntity { MockTestId=testId, DisplayOrder=request.DisplayOrder, Section=request.Section.Trim(), Text=request.Text.Trim(), Series=request.Series, OptionA=request.OptionA, OptionB=request.OptionB, OptionC=request.OptionC, OptionD=request.OptionD, CorrectAnswer=request.CorrectAnswer.ToUpperInvariant(), Explanation=request.Explanation ?? "", Difficulty=request.Difficulty ?? "Medium" };
        db.TestQuestions.Add(item); await db.SaveChangesAsync(); return Created($"api/admin/questions/{item.Id}", item);
    }

    [HttpPut("questions/{id:int}")]
    public async Task<IActionResult> UpdateQuestion(int id, QuestionRequest request)
    {
        var item=await db.TestQuestions.FindAsync(id); if(item is null)return NotFound();
        item.DisplayOrder=request.DisplayOrder; item.Section=request.Section.Trim(); item.Text=request.Text.Trim(); item.Series=request.Series; item.OptionA=request.OptionA; item.OptionB=request.OptionB; item.OptionC=request.OptionC; item.OptionD=request.OptionD; item.CorrectAnswer=request.CorrectAnswer.ToUpperInvariant(); item.Explanation=request.Explanation ?? ""; item.Difficulty=request.Difficulty ?? "Medium";
        await db.SaveChangesAsync(); return Ok(item);
    }

    [HttpDelete("questions/{id:int}")]
    public async Task<IActionResult> DeleteQuestion(int id) { var item=await db.TestQuestions.FindAsync(id); if(item is null)return NotFound(); db.TestQuestions.Remove(item); await db.SaveChangesAsync(); return NoContent(); }

    public record CategoryRequest(string Name, string Description, string? Icon, string? Accent, bool IsActive=true);
    public record TestRequest(int CategoryId, string Title, int TotalQuestions, int TotalMarks, int DurationMinutes, decimal NegativeMarking=.5m, string? Tag=null, bool IsFree=true, bool IsPublished=true);
    public record QuestionRequest(int DisplayOrder, string Section, string Text, string? Series, string OptionA, string OptionB, string OptionC, string OptionD, string CorrectAnswer, string? Explanation, string? Difficulty);
}