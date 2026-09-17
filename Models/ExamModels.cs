namespace GenericTestWebApi.Models;

public record ExamCategory(int Id, string Name, string Description, string Icon, string Accent);

public record MockTest(
    int Id,
    string Category,
    string Title,
    int Questions,
    int Marks,
    int DurationMinutes,
    string Tag,
    bool IsFree = true);

public record TestQuestion(
    int Id,
    int TestId,
    string Section,
    string Text,
    string? Series,
    IReadOnlyList<string> Options,
    string CorrectAnswer,
    string Explanation);

public record SubmitAnswer(int QuestionId, string? Answer, bool MarkedForReview = false);

public record SubmitTestRequest(IReadOnlyList<SubmitAnswer> Answers, int TimeTakenSeconds);

public record SectionResult(string Section, int Correct, int Incorrect, int Unattempted, double Score, int Total, int Accuracy);
