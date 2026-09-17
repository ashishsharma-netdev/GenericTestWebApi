using System.Security.Claims;
using GenericTestWebApi.Data;
using GenericTestWebApi.Entities;
using Microsoft.AspNetCore.Authorization;
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
            .Select(x => new { x.Id, category = x.ExamCategory.Name, title = x.Title, questions = x.TotalQuestions, marks = x.TotalMarks, durationMinutes = x.DurationMinutes, x.NegativeMarking, x.Tag, x.IsFree })
            .FirstOrDefaultAsync();
        if (test is null) return NotFound(new { message = "Test not found." });
        if (!test.IsFree && !await HasPremiumAccessAsync()) return StatusCode(403, new { message = "This test is available to Premium members only.", requiresPremium = true });
        return Ok(test);
    }

    [HttpGet("{testId:int}/questions")]
    public async Task<IActionResult> GetQuestions(int testId)
    {
        var test = await db.MockTests.AsNoTracking().Where(x => x.Id == testId).Select(x => new { x.Id, x.IsFree }).FirstOrDefaultAsync();
        if (test is null) return NotFound(new { message = "Test not found." });
        if (!test.IsFree && !await HasPremiumAccessAsync()) return StatusCode(403, new { message = "This test is available to Premium members only.", requiresPremium = true });
        var q = await db.TestQuestions.AsNoTracking().Where(x => x.MockTestId == testId).OrderBy(x => x.DisplayOrder)
            .Select(x => new { x.Id, testId = x.MockTestId, x.Section, x.Text, x.Series, options = new[] { x.OptionA, x.OptionB, x.OptionC, x.OptionD }, correctAnswer = x.CorrectAnswer, x.Explanation, x.Difficulty }).ToListAsync();
        return Ok(q);
    }

    [HttpPost("{testId:int}/submit")]
    public async Task<IActionResult> Submit(int testId, [FromBody] SubmitTestRequest request)
    {
        var test = await db.MockTests.AsNoTracking().Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == testId);
        if (test is null) return NotFound(new { message = "Test not found." });
        if (!test.IsFree && !await HasPremiumAccessAsync()) return StatusCode(403, new { message = "This test is available to Premium members only.", requiresPremium = true });

        var answers = request.Answers ?? [];
        var sections = test.Questions.GroupBy(q => q.Section).Select(g =>
        {
            var correct = g.Count(q => answers.Any(a => a.QuestionId == q.Id && string.Equals(a.Answer, q.CorrectAnswer, StringComparison.OrdinalIgnoreCase)));
            var answered = g.Count(q => answers.Any(a => a.QuestionId == q.Id && !string.IsNullOrWhiteSpace(a.Answer)));
            var incorrect = answered - correct;
            var unattempted = g.Count() - answered;
            var score = Math.Max(0m, correct - incorrect * test.NegativeMarking);
            var accuracy = answered == 0 ? 0 : (int)Math.Round(correct * 100.0 / answered);
            return new { section = g.Key, correct, incorrect, unattempted, score, total = g.Count(), accuracy };
        }).ToList();
        var correctTotal = sections.Sum(x => x.correct);
        var incorrectTotal = sections.Sum(x => x.incorrect);
        var total = test.Questions.Count;
        var scoreTotal = Math.Max(0m, correctTotal - incorrectTotal * test.NegativeMarking);
        var percentage = total == 0 ? 0 : Math.Round(scoreTotal * 100 / total, 2);

        int? userId = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) userId = uid;
        int? attemptId = null;
        if (userId.HasValue)
        {
            var attempt = new TestAttemptEntity { UserId = userId.Value, MockTestId = testId, StartedAtUtc = DateTime.UtcNow.AddSeconds(-Math.Max(0, request.TimeTakenSeconds)), SubmittedAtUtc = DateTime.UtcNow, TimeTakenSeconds = request.TimeTakenSeconds, Correct = correctTotal, Incorrect = incorrectTotal, Unattempted = total - correctTotal - incorrectTotal, Score = scoreTotal, Percentage = percentage };
            db.TestAttempts.Add(attempt); await db.SaveChangesAsync();
            foreach (var a in answers)
            {
                var q = test.Questions.FirstOrDefault(x => x.Id == a.QuestionId); if (q == null) continue;
                db.TestAttemptAnswers.Add(new TestAttemptAnswerEntity { TestAttemptId = attempt.Id, QuestionId = q.Id, Answer = a.Answer, IsCorrect = !string.IsNullOrWhiteSpace(a.Answer) && string.Equals(q.CorrectAnswer, a.Answer, StringComparison.OrdinalIgnoreCase), MarkedForReview = a.MarkedForReview });
            }
            await db.SaveChangesAsync(); attemptId = attempt.Id;
        }
        return Ok(new { testId, attemptId, score = scoreTotal, maxScore = test.TotalMarks, percentage, correct = correctTotal, incorrect = incorrectTotal, unattempted = total - correctTotal - incorrectTotal, timeTakenSeconds = request.TimeTakenSeconds, sections });
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return Unauthorized();
        return Ok(await db.TestAttempts.AsNoTracking().Where(x => x.UserId == uid).OrderByDescending(x => x.SubmittedAtUtc).Select(x => new { x.Id, testId = x.MockTestId, test = x.MockTest.Title, x.StartedAtUtc, x.SubmittedAtUtc, x.TimeTakenSeconds, x.Correct, x.Incorrect, x.Unattempted, x.Score, x.Percentage }).ToListAsync());
    }

    private async Task<bool> HasPremiumAccessAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return false;
        return await db.Subscriptions.AnyAsync(x => x.UserId == userId && x.Status == "Active" && x.ExpiresAtUtc > DateTime.UtcNow);
    }

    public record SubmitTestRequest(List<SubmitAnswerDto>? Answers, int TimeTakenSeconds);
    public record SubmitAnswerDto(int QuestionId, string? Answer, bool MarkedForReview = false);
}
