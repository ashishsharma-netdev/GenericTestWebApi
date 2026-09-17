using Microsoft.AspNetCore.Mvc;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController : ControllerBase
{
    private static readonly string[] Categories = ["SSC", "Railway", "Bank", "State Exam", "UPSC", "CAT", "CTET"];

    [HttpGet("categories")]
    public IActionResult GetCategories() => Ok(Categories.Select((name, index) => new { id = index + 1, name }));

    [HttpGet("{category}/tests")]
    public IActionResult GetTests(string category)
    {
        var tests = new[]
        {
            new { id=1, title=$"{category} Full Mock Test 01", questions=200, marks=200, durationMinutes=60, tag="Latest Pattern" },
            new { id=2, title=$"{category} Full Mock Test 02", questions=200, marks=200, durationMinutes=60, tag="" },
            new { id=3, title=$"{category} Previous Year Paper (2023)", questions=200, marks=200, durationMinutes=60, tag="PYQ" },
            new { id=4, title=$"{category} Sectional Test - Quant", questions=50, marks=50, durationMinutes=30, tag="" },
            new { id=5, title=$"{category} Sectional Test - Reasoning", questions=50, marks=50, durationMinutes=30, tag="" },
            new { id=6, title=$"{category} Sectional Test - English", questions=50, marks=50, durationMinutes=30, tag="" }
        };
        return Ok(tests);
    }
}
