using GenericTestWebApi.Data;
using GenericTestWebApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestsController : ControllerBase
{
    [HttpGet("{testId:int}")]
    public IActionResult GetTest(int testId)
    {
        var test = TestData.Categories.SelectMany(c => TestData.GetTests(c.Name)).FirstOrDefault(x => x.Id == testId);
        return test is null ? NotFound(new { message = "Test not found." }) : Ok(test);
    }

    [HttpGet("{testId:int}/questions")]
    public IActionResult GetQuestions(int testId)
    {
        var test = TestData.Categories.SelectMany(c => TestData.GetTests(c.Name)).FirstOrDefault(x => x.Id == testId);
        if (test is null) return NotFound(new { message = "Test not found." });
        return Ok(TestData.GetQuestions(testId));
    }

    [HttpPost("{testId:int}/submit")]
    public IActionResult Submit(int testId, [FromBody] SubmitTestRequest request)
    {
        var questions = TestData.GetQuestions(testId);
        if (questions.Count == 0) return NotFound(new { message = "Test not found." });

        var answers = request.Answers.ToDictionary(x => x.QuestionId, x => x.Answer);
        var sections = questions.GroupBy(q => q.Section).Select(group =>
        {
            var correct = group.Count(q => answers.TryGetValue(q.Id, out var a) && a == q.CorrectAnswer);
            var answered = group.Count(q => answers.TryGetValue(q.Id, out var a) && !string.IsNullOrWhiteSpace(a));
            var incorrect = answered - correct;
            var unattempted = group.Count() - answered;
            var score = correct - incorrect * 0.5;
            var accuracy = answered == 0 ? 0 : (int)Math.Round(correct * 100.0 / answered);
            return new SectionResult(group.Key, correct, incorrect, unattempted, score, group.Count(), accuracy);
        }).ToList();

        var correctTotal = sections.Sum(x => x.Correct);
        var incorrectTotal = sections.Sum(x => x.Incorrect);
        var totalQuestions = questions.Count;
        var scoreTotal = Math.Max(0, correctTotal - incorrectTotal * 0.5);
        var percentage = totalQuestions == 0 ? 0 : Math.Round(scoreTotal * 100 / totalQuestions, 2);

        return Ok(new
        {
            testId,
            score = scoreTotal,
            maxScore = totalQuestions,
            percentage,
            rank = "—",
            timeTakenSeconds = request.TimeTakenSeconds,
            sections,
            correct = correctTotal,
            incorrect = incorrectTotal,
            unattempted = totalQuestions - correctTotal - incorrectTotal
        });
    }
}
