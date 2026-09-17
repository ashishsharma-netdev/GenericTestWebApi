using Microsoft.AspNetCore.Mvc;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestsController : ControllerBase
{
    [HttpGet("{testId:int}/questions")]
    public IActionResult GetQuestions(int testId) => Ok(new[]
    {
        new { id=12, section="Reasoning", text="Which of the following numbers will replace the question mark (?) in the series?", series="2, 6, 12, 20, 30, ?", options=new[]{"40","42","44","46"}, correctAnswer="42" }
    });

    [HttpPost("{testId:int}/submit")]
    public IActionResult Submit(int testId, [FromBody] SubmitRequest request) => Ok(new
    {
        testId,
        score = 152,
        percentage = 76,
        rank = "1,245 / 12,500",
        timeTaken = "58m 12s",
        submittedAnswers = request.Answers
    });

    public record SubmitRequest(Dictionary<int,string> Answers);
}
