namespace GenericTestWebApi.Entities;

public class TestQuestionEntity
{
    public int Id { get; set; }
    public int MockTestId { get; set; }
    public int DisplayOrder { get; set; }
    public string Section { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Series { get; set; }
    public string OptionA { get; set; } = "";
    public string OptionB { get; set; } = "";
    public string OptionC { get; set; } = "";
    public string OptionD { get; set; } = "";
    public string CorrectAnswer { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string Difficulty { get; set; } = "Medium";
    public MockTestEntity MockTest { get; set; } = null!;
}