using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles="Admin")]
public class AdminController(TestPrepDbContext db) : ControllerBase
{
    [HttpGet("stats")] public async Task<IActionResult> Stats()=>Ok(new{exams=await db.ExamCategories.CountAsync(),tests=await db.MockTests.CountAsync(),questions=await db.TestQuestions.CountAsync(),publishedTests=await db.MockTests.CountAsync(x=>x.IsPublished)});
    [HttpGet("categories")] public async Task<IActionResult> Categories()=>Ok(await db.ExamCategories.OrderBy(x=>x.Id).ToListAsync());
    [HttpPost("categories")] public async Task<IActionResult> CreateCategory(CategoryRequest r){if(await db.ExamCategories.AnyAsync(x=>x.Name==r.Name))return Conflict(new{message="Category already exists."});var x=new ExamCategoryEntity{Name=r.Name.Trim(),Description=r.Description.Trim(),Icon=r.Icon??"◉",Accent=r.Accent??"#1264d8"};db.ExamCategories.Add(x);await db.SaveChangesAsync();return Created($"api/admin/categories/{x.Id}",x);}
    [HttpPut("categories/{id:int}")] public async Task<IActionResult> UpdateCategory(int id,CategoryRequest r){var x=await db.ExamCategories.FindAsync(id);if(x is null)return NotFound();x.Name=r.Name.Trim();x.Description=r.Description.Trim();x.Icon=r.Icon??x.Icon;x.Accent=r.Accent??x.Accent;x.IsActive=r.IsActive;await db.SaveChangesAsync();return Ok(x);}
    [HttpGet("tests")] public async Task<IActionResult> Tests()=>Ok(await db.MockTests.Include(x=>x.ExamCategory).OrderByDescending(x=>x.Id).Select(x=>new{x.Id,categoryId=x.ExamCategoryId,category=x.ExamCategory.Name,title=x.Title,questions=x.TotalQuestions,marks=x.TotalMarks,x.DurationMinutes,x.NegativeMarking,x.Tag,x.IsFree,x.IsPublished}).ToListAsync());
    [HttpPost("tests")] public async Task<IActionResult> CreateTest(TestRequest r){if(!await db.ExamCategories.AnyAsync(x=>x.Id==r.CategoryId))return BadRequest(new{message="Invalid exam category."});var x=new MockTestEntity{ExamCategoryId=r.CategoryId,Title=r.Title.Trim(),TotalQuestions=r.TotalQuestions,TotalMarks=r.TotalMarks,DurationMinutes=r.DurationMinutes,NegativeMarking=r.NegativeMarking,Tag=r.Tag??"",IsFree=r.IsFree,IsPublished=r.IsPublished};db.MockTests.Add(x);await db.SaveChangesAsync();return Created($"api/admin/tests/{x.Id}",x);}
    [HttpPut("tests/{id:int}")] public async Task<IActionResult> UpdateTest(int id,TestRequest r){var x=await db.MockTests.FindAsync(id);if(x is null)return NotFound();x.ExamCategoryId=r.CategoryId;x.Title=r.Title.Trim();x.TotalQuestions=r.TotalQuestions;x.TotalMarks=r.TotalMarks;x.DurationMinutes=r.DurationMinutes;x.NegativeMarking=r.NegativeMarking;x.Tag=r.Tag??"";x.IsFree=r.IsFree;x.IsPublished=r.IsPublished;await db.SaveChangesAsync();return Ok(x);}
    [HttpDelete("tests/{id:int}")] public async Task<IActionResult> DeleteTest(int id){var x=await db.MockTests.FindAsync(id);if(x is null)return NotFound();db.MockTests.Remove(x);await db.SaveChangesAsync();return NoContent();}
    [HttpGet("tests/{testId:int}/questions")] public async Task<IActionResult> Questions(int testId)=>Ok(await db.TestQuestions.Where(x=>x.MockTestId==testId).OrderBy(x=>x.DisplayOrder).ToListAsync());
    [HttpPost("tests/{testId:int}/questions")] public async Task<IActionResult> CreateQuestion(int testId,QuestionRequest r){if(!await db.MockTests.AnyAsync(x=>x.Id==testId))return NotFound();var x=new TestQuestionEntity{MockTestId=testId,DisplayOrder=r.DisplayOrder,Section=r.Section.Trim(),Text=r.Text.Trim(),Series=r.Series,OptionA=r.OptionA,OptionB=r.OptionB,OptionC=r.OptionC,OptionD=r.OptionD,CorrectAnswer=r.CorrectAnswer.ToUpperInvariant(),Explanation=r.Explanation??"",Difficulty=r.Difficulty??"Medium"};db.TestQuestions.Add(x);await db.SaveChangesAsync();return Created($"api/admin/questions/{x.Id}",x);}
    [HttpPut("questions/{id:int}")] public async Task<IActionResult> UpdateQuestion(int id,QuestionRequest r){var x=await db.TestQuestions.FindAsync(id);if(x is null)return NotFound();x.DisplayOrder=r.DisplayOrder;x.Section=r.Section.Trim();x.Text=r.Text.Trim();x.Series=r.Series;x.OptionA=r.OptionA;x.OptionB=r.OptionB;x.OptionC=r.OptionC;x.OptionD=r.OptionD;x.CorrectAnswer=r.CorrectAnswer.ToUpperInvariant();x.Explanation=r.Explanation??"";x.Difficulty=r.Difficulty??"Medium";await db.SaveChangesAsync();return Ok(x);}
    [HttpDelete("questions/{id:int}")] public async Task<IActionResult> DeleteQuestion(int id){var x=await db.TestQuestions.FindAsync(id);if(x is null)return NotFound();db.TestQuestions.Remove(x);await db.SaveChangesAsync();return NoContent();}
    public record CategoryRequest(string Name,string Description,string? Icon,string? Accent,bool IsActive=true);
    public record TestRequest(int CategoryId,string Title,int TotalQuestions,int TotalMarks,int DurationMinutes,decimal NegativeMarking=.5m,string? Tag=null,bool IsFree=true,bool IsPublished=true);
    public record QuestionRequest(int DisplayOrder,string Section,string Text,string? Series,string OptionA,string OptionB,string OptionC,string OptionD,string CorrectAnswer,string? Explanation,string? Difficulty);
}