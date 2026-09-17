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
        var access = await GetAccessibleTestAsync(testId);
        if (access.Error is not null) return access.Error;
        var q = await db.TestQuestions.AsNoTracking().Where(x => x.MockTestId == testId).OrderBy(x => x.DisplayOrder)
            .Select(x => new { x.Id, testId = x.MockTestId, x.DisplayOrder, x.Section, x.Text, x.Series, options = new[] { x.OptionA, x.OptionB, x.OptionC, x.OptionD }, x.Explanation, x.Difficulty })
            .ToListAsync();
        return Ok(q);
    }

    [Authorize]
    [HttpPost("{testId:int}/start")]
    public async Task<IActionResult> Start(int testId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var access = await GetAccessibleTestAsync(testId);
        if (access.Error is not null) return access.Error;
        var test = access.Test!;
        if (!await db.TestQuestions.AnyAsync(x => x.MockTestId == testId)) return BadRequest(new { message = "This test has no questions yet." });

        var active = await db.TestAttempts.FirstOrDefaultAsync(x => x.UserId == userId && x.MockTestId == testId && !x.IsSubmitted);
        if (active is not null)
        {
            var elapsed = Math.Max(0, (int)(DateTime.UtcNow - active.StartedAtUtc).TotalSeconds);
            var remaining = Math.Max(0, test.DurationMinutes * 60 - elapsed);
            return Ok(new { attemptId = active.Id, testId, startedAtUtc = active.StartedAtUtc, elapsedSeconds = elapsed, remainingSeconds = remaining, resumed = true });
        }

        var attempt = new TestAttemptEntity { UserId = userId, MockTestId = testId, StartedAtUtc = DateTime.UtcNow, IsSubmitted = false };
        db.TestAttempts.Add(attempt);
        await db.SaveChangesAsync();
        return Ok(new { attemptId = attempt.Id, testId, startedAtUtc = attempt.StartedAtUtc, elapsedSeconds = 0, remainingSeconds = test.DurationMinutes * 60, resumed = false });
    }

    [Authorize]
    [HttpPut("attempts/{attemptId:int}/answers")]
    public async Task<IActionResult> SaveAnswer(int attemptId, [FromBody] SaveAnswerRequest request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var attempt = await db.TestAttempts.Include(x => x.MockTest).ThenInclude(x => x.Questions).FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId);
        if (attempt is null) return NotFound(new { message = "Attempt not found." });
        if (attempt.IsSubmitted) return Conflict(new { message = "This test attempt has already been submitted." });
        if (request.QuestionId <= 0 || !attempt.MockTest.Questions.Any(q => q.Id == request.QuestionId)) return BadRequest(new { message = "Question does not belong to this test." });
        var elapsed = (int)Math.Max(0, (DateTime.UtcNow - attempt.StartedAtUtc).TotalSeconds);
        if (elapsed >= attempt.MockTest.DurationMinutes * 60) return Conflict(new { message = "The test time has expired.", timeExpired = true });

        var existing = await db.TestAttemptAnswers.FirstOrDefaultAsync(x => x.TestAttemptId == attemptId && x.QuestionId == request.QuestionId);
        if (existing is null)
        {
            db.TestAttemptAnswers.Add(new TestAttemptAnswerEntity { TestAttemptId = attemptId, QuestionId = request.QuestionId, Answer = NormalizeAnswer(request.Answer), MarkedForReview = request.MarkedForReview });
        }
        else
        {
            existing.Answer = NormalizeAnswer(request.Answer);
            existing.MarkedForReview = request.MarkedForReview;
        }
        await db.SaveChangesAsync();
        return Ok(new { attemptId, questionId = request.QuestionId, saved = true, elapsedSeconds = elapsed, remainingSeconds = Math.Max(0, attempt.MockTest.DurationMinutes * 60 - elapsed) });
    }

    [Authorize]
    [HttpGet("attempts/{attemptId:int}")]
    public async Task<IActionResult> GetAttempt(int attemptId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var attempt = await db.TestAttempts.AsNoTracking().Include(x => x.MockTest).ThenInclude(x => x.Questions).Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId);
        if (attempt is null) return NotFound(new { message = "Attempt not found." });
        var elapsed = Math.Max(0, (int)((attempt.SubmittedAtUtc ?? DateTime.UtcNow) - attempt.StartedAtUtc).TotalSeconds);
        return Ok(new { attemptId = attempt.Id, testId = attempt.MockTestId, isSubmitted = attempt.IsSubmitted, startedAtUtc = attempt.StartedAtUtc, submittedAtUtc = attempt.SubmittedAtUtc, elapsedSeconds = elapsed, remainingSeconds = attempt.IsSubmitted ? 0 : Math.Max(0, attempt.MockTest.DurationMinutes * 60 - elapsed), answers = attempt.Answers.Select(a => new { questionId = a.QuestionId, answer = a.Answer, markedForReview = a.MarkedForReview }) });
    }

    [Authorize]
    [HttpPost("attempts/{attemptId:int}/submit")]
    public async Task<IActionResult> SubmitAttempt(int attemptId, [FromBody] SubmitAttemptRequest? request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var attempt = await db.TestAttempts.Include(x => x.MockTest).ThenInclude(x => x.Questions).Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId);
        if (attempt is null) return NotFound(new { message = "Attempt not found." });
        if (attempt.IsSubmitted) return await BuildResultAsync(attempt);

        if (request?.Answers is not null)
        {
            foreach (var incoming in request.Answers)
            {
                var question = attempt.MockTest.Questions.FirstOrDefault(q => q.Id == incoming.QuestionId);
                if (question is null) continue;
                var existing = attempt.Answers.FirstOrDefault(a => a.QuestionId == incoming.QuestionId);
                if (existing is null)
                {
                    attempt.Answers.Add(new TestAttemptAnswerEntity { TestAttemptId = attempt.Id, QuestionId = incoming.QuestionId, Answer = NormalizeAnswer(incoming.Answer), MarkedForReview = incoming.MarkedForReview });
                }
                else
                {
                    existing.Answer = NormalizeAnswer(incoming.Answer);
                    existing.MarkedForReview = incoming.MarkedForReview;
                }
            }
        }

        var elapsed = Math.Max(0, (int)(DateTime.UtcNow - attempt.StartedAtUtc).TotalSeconds);
        var maxSeconds = attempt.MockTest.DurationMinutes * 60;
        var effectiveTime = Math.Min(elapsed, maxSeconds);
        var correct = 0; var incorrect = 0;
        foreach (var answer in attempt.Answers)
        {
            var question = attempt.MockTest.Questions.FirstOrDefault(q => q.Id == answer.QuestionId);
            if (question is null || string.IsNullOrWhiteSpace(answer.Answer)) { answer.IsCorrect = false; continue; }
            answer.IsCorrect = string.Equals(answer.Answer, question.CorrectAnswer, StringComparison.OrdinalIgnoreCase);
            if (answer.IsCorrect) correct++; else incorrect++;
        }
        var total = attempt.MockTest.Questions.Count;
        var unattempted = Math.Max(0, total - correct - incorrect);
        var score = Math.Max(0m, correct - incorrect * attempt.MockTest.NegativeMarking);
        var percentage = total == 0 ? 0m : Math.Round(score * 100m / total, 2);
        attempt.TimeTakenSeconds = effectiveTime;
        attempt.Correct = correct;
        attempt.Incorrect = incorrect;
        attempt.Unattempted = unattempted;
        attempt.Score = score;
        attempt.Percentage = percentage;
        attempt.SubmittedAtUtc = DateTime.UtcNow;
        attempt.IsSubmitted = true;
        await db.SaveChangesAsync();
        return await BuildResultAsync(attempt);
    }

    [Authorize]
    [HttpGet("attempts/{attemptId:int}/result")]
    public async Task<IActionResult> Result(int attemptId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var attempt = await db.TestAttempts.Include(x => x.MockTest).ThenInclude(x => x.Questions).Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.UserId == userId);
        if (attempt is null) return NotFound(new { message = "Attempt not found." });
        if (!attempt.IsSubmitted) return Conflict(new { message = "This test has not been submitted yet.", submitted = false });
        return await BuildResultAsync(attempt);
    }

    [Authorize]
    [HttpPost("{testId:int}/submit")]
    public async Task<IActionResult> LegacySubmit(int testId, [FromBody] SubmitTestRequest request)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var access = await GetAccessibleTestAsync(testId);
        if (access.Error is not null) return access.Error;
        var attempt = await db.TestAttempts.Include(x => x.MockTest).ThenInclude(x => x.Questions).Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.MockTestId == testId && !x.IsSubmitted);
        if (attempt is null)
        {
            attempt = new TestAttemptEntity { UserId = userId, MockTestId = testId, StartedAtUtc = DateTime.UtcNow.AddSeconds(-Math.Max(0, request.TimeTakenSeconds)) };
            db.TestAttempts.Add(attempt);
            await db.SaveChangesAsync();
        }
        return await SubmitAttemptInternal(attempt, request.Answers);
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return Unauthorized();
        return Ok(await db.TestAttempts.AsNoTracking().Where(x => x.UserId == uid && x.IsSubmitted).OrderByDescending(x => x.SubmittedAtUtc).Select(x => new { x.Id, testId = x.MockTestId, test = x.MockTest.Title, x.StartedAtUtc, x.SubmittedAtUtc, x.TimeTakenSeconds, x.Correct, x.Incorrect, x.Unattempted, x.Score, x.Percentage }).ToListAsync());
    }

    private async Task<IActionResult> SubmitAttemptInternal(TestAttemptEntity attempt, List<SubmitAnswerDto>? answers)
    {
        if (answers is not null)
            foreach (var incoming in answers)
            {
                var q = attempt.MockTest.Questions.FirstOrDefault(x => x.Id == incoming.QuestionId); if (q is null) continue;
                var existing = attempt.Answers.FirstOrDefault(x => x.QuestionId == incoming.QuestionId);
                if (existing is null) attempt.Answers.Add(new TestAttemptAnswerEntity { TestAttemptId = attempt.Id, QuestionId = q.Id, Answer = NormalizeAnswer(incoming.Answer), MarkedForReview = incoming.MarkedForReview });
                else { existing.Answer = NormalizeAnswer(incoming.Answer); existing.MarkedForReview = incoming.MarkedForReview; }
            }
        foreach (var a in attempt.Answers) { var q = attempt.MockTest.Questions.FirstOrDefault(x => x.Id == a.QuestionId); a.IsCorrect = q != null && !string.IsNullOrWhiteSpace(a.Answer) && string.Equals(q.CorrectAnswer, a.Answer, StringComparison.OrdinalIgnoreCase); }
        var total = attempt.MockTest.Questions.Count; var correct = attempt.Answers.Count(x => x.IsCorrect); var answered = attempt.Answers.Count(x => !string.IsNullOrWhiteSpace(x.Answer)); var incorrect = Math.Max(0, answered - correct);
        attempt.Correct = correct; attempt.Incorrect = incorrect; attempt.Unattempted = total - answered; attempt.Score = Math.Max(0m, correct - incorrect * attempt.MockTest.NegativeMarking); attempt.Percentage = total == 0 ? 0 : Math.Round(attempt.Score * 100m / total, 2); attempt.TimeTakenSeconds = Math.Min((int)Math.Max(0, (DateTime.UtcNow - attempt.StartedAtUtc).TotalSeconds), attempt.MockTest.DurationMinutes * 60); attempt.SubmittedAtUtc = DateTime.UtcNow; attempt.IsSubmitted = true;
        await db.SaveChangesAsync(); return await BuildResultAsync(attempt);
    }

    private async Task<IActionResult> BuildResultAsync(TestAttemptEntity attempt)
    {
        var sections = attempt.MockTest.Questions.GroupBy(q => q.Section).Select(g =>
        {
            var answers = g.Select(q => attempt.Answers.FirstOrDefault(a => a.QuestionId == q.Id)).ToList();
            var answered = answers.Count(a => !string.IsNullOrWhiteSpace(a?.Answer)); var correct = answers.Count(a => a?.IsCorrect == true); var incorrect = Math.Max(0, answered - correct);
            return new { section = g.Key, total = g.Count(), correct, incorrect, unattempted = g.Count() - answered, score = Math.Max(0m, correct - incorrect * attempt.MockTest.NegativeMarking), accuracy = answered == 0 ? 0 : (int)Math.Round(correct * 100.0 / answered) };
        }).ToList();
        var questionResults = attempt.MockTest.Questions.OrderBy(q => q.DisplayOrder).Select(q =>
        {
            var a = attempt.Answers.FirstOrDefault(x => x.QuestionId == q.Id);
            return new { questionId = q.Id, q.DisplayOrder, q.Section, answer = a?.Answer, isCorrect = a?.IsCorrect ?? false, markedForReview = a?.MarkedForReview ?? false, correctAnswer = q.CorrectAnswer, explanation = q.Explanation };
        }).ToList();
        return Ok(new { attemptId = attempt.Id, testId = attempt.MockTestId, test = attempt.MockTest.Title, submittedAtUtc = attempt.SubmittedAtUtc, timeTakenSeconds = attempt.TimeTakenSeconds, durationMinutes = attempt.MockTest.DurationMinutes, totalQuestions = attempt.MockTest.Questions.Count, attempt.Correct, attempt.Incorrect, attempt.Unattempted, score = attempt.Score, maxScore = attempt.MockTest.TotalMarks, percentage = attempt.Percentage, negativeMarking = attempt.MockTest.NegativeMarking, sections, questions = questionResults });
    }

    private async Task<(MockTestEntity? Test, IActionResult? Error)> GetAccessibleTestAsync(int testId)
    {
        var test = await db.MockTests.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == testId);
        if (test is null) return (null, NotFound(new { message = "Test not found." }));
        if (!test.IsFree && !await HasPremiumAccessAsync()) return (null, StatusCode(403, new { message = "This test is available to Premium members only.", requiresPremium = true }));
        return (test, null);
    }

    private async Task<bool> HasPremiumAccessAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return false;
        return await db.Subscriptions.AnyAsync(x => x.UserId == userId && x.Status == "Active" && x.ExpiresAtUtc > DateTime.UtcNow);
    }

    private static string? NormalizeAnswer(string? answer) => string.IsNullOrWhiteSpace(answer) ? null : answer.Trim().ToUpperInvariant();

    public record SaveAnswerRequest(int QuestionId, string? Answer, bool MarkedForReview = false);
    public record SubmitAttemptRequest(List<SubmitAnswerDto>? Answers);
    public record SubmitTestRequest(List<SubmitAnswerDto>? Answers, int TimeTakenSeconds);
    public record SubmitAnswerDto(int QuestionId, string? Answer, bool MarkedForReview = false);
}
