namespace GenericTestWebApi.Entities;

public class MockTestEntity
{
    public int Id { get; set; }
    public int ExamCategoryId { get; set; }
    public string Title { get; set; } = "";
    public int TotalQuestions { get; set; }
    public int TotalMarks { get; set; }
    public int DurationMinutes { get; set; }
    public decimal NegativeMarking { get; set; } = 0.5m;
    public string Tag { get; set; } = "";
    public bool IsFree { get; set; } = true;
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ExamCategoryEntity ExamCategory { get; set; } = null!;
    public ICollection<TestQuestionEntity> Questions { get; set; } = new List<TestQuestionEntity>();
}