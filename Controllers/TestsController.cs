using GenericTestWebApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestsController(TestPrepDbContext db) : ControllerBase
{
    [HttpGet("{testId:int}")]
    public async Task<IActionResult> GetTest(int testId)
    {
        var test = await db.MockTests.AsNoTracking().Where(x => x.Id == testId)
            .Select(x => new { x.Id, category = x.ExamCategory.Name, title = x.Title, questions = x.TotalQuestions, marks = x.TotalMarks, durationMinutes = x.DurationMinutes, x.NegativeMarking, x.Tag, x.IsFree }).FirstOrDefaultAsync();
        return test is null ? NotFound(new { message = "Test not found." }) : Ok(test);
    }

    [HttpGet("{testId:int}/questions")]
    public async Task<IActionResult> GetQuestions(int testId)
    {
        if (!await db.MockTests.AnyAsync(x => x.Id == testId)) return NotFound(new { message = "Test not found." });
        var questions = await db.TestQuestions.AsNoTracking().Where(x => x.MockTestId == testId).OrderBy(x => x.DisplayOrder)
            .Select(x => new { x.Id, testId = x.MockTestId, x.Section, x.Text, x.Series, options = new[] { x.OptionA, x.OptionB, x.OptionC, x.OptionD }, correctAnswer = x.CorrectAnswer, x.Explanation, x.Difficulty }).ToListAsync();
        return Ok(questions);
    }

    [HttpPost("{testId:int}/submit")]
    public async Task<IActionResult> Submit(int testId, [FromBody] SubmitTestRequest request)
    {
        var test = await db.MockTests.AsNoTracking().Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == testId);
        if (test is null) return NotFound(new { message = "Test not found." });
        var answers = request.Answers ?? [];
        var sections = test.Questions.GroupBy(q => q.Section).Select(group =>
        {
            var correct = group.Count(q => answers.Any(a => a.QuestionId == q.Id && string.Equals(a.Answer, q.CorrectAnswer, StringComparison.OrdinalIgnoreCase)));
            var answered = group.Count(q => answers.Any(a => a.QuestionId == q.Id && !string.IsNullOrWhiteSpace(a.Answer)));
            var incorrect = answered - correct;
            var unattempted = group.Count() - answered;
            var score = Math.Max(0m, correct - incorrect * test.NegativeMarking);
            var accuracy = answered == 0 ? 0 : (int)Math.Round(correct * 100.0 / answered);
            return new { section = group.Key, correct, incorrect, unattempted, score, total = group.Count(), accuracy };
        }).ToList();
        var correctTotal = sections.Sum(x => x.correct); var incorrectTotal = sections.Sum(x => x.incorrect); var total = test.Questions.Count;
        var scoreTotal = Math.Max(0m, correctTotal - incorrectTotal * test.NegativeMarking);
        return Ok(new { testId, score = scoreTotal, maxScore = test.TotalMarks, percentage = total == 0 ? 0 : Math.Round(scoreTotal * 100 / total, 2), correct = correctTotal, incorrect = incorrectTotal, unattempted = total - correctTotal - incorrectTotal, timeTakenSeconds = request.TimeTakenSeconds, sections });
    }

    public record SubmitTestRequest(List<SubmitAnswerDto>? Answers, int TimeTakenSeconds);
    public record SubmitAnswerDto(int QuestionId, string? Answer, bool MarkedForReview = false);
}