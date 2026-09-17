using GenericTestWebApi.Data;
using Microsoft.AspNetCore.Mvc;

namespace GenericTestWebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController : ControllerBase
{
    [HttpGet("categories")]
    public IActionResult GetCategories() => Ok(TestData.Categories);

    [HttpGet("{category}/tests")]
    public IActionResult GetTests(string category)
    {
        if (!TestData.Categories.Any(x => x.Name.Equals(category, StringComparison.OrdinalIgnoreCase)))
            return NotFound(new { message = $"Exam category '{category}' was not found." });

        return Ok(TestData.GetTests(category));
    }
}
