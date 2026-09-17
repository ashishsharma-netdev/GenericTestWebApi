using GenericTestWebApi.Models;

namespace GenericTestWebApi.Data;

public static class TestData
{
    public static readonly IReadOnlyList<ExamCategory> Categories =
    [
        new(1, "SSC", "CGL, CHSL, MTS, GD, Stenographer", "◉", "#e33434"),
        new(2, "Railway", "RRB NTPC, Group D, ALP, JE", "◆", "#18a957"),
        new(3, "Bank", "IBPS, SBI, RRB, PO, Clerk", "▥", "#1877d2"),
        new(4, "State Exam", "PSC, Police, SI, TET and more", "♜", "#f07824"),
        new(5, "UPSC", "Civil Services (IAS/IPS/IFS)", "♛", "#6546d9"),
        new(6, "CAT", "Common Admission Test", "∞", "#db3b91"),
        new(7, "CTET", "Central Teacher Eligibility Test", "◌", "#12a9bb")
    ];

    public static IReadOnlyList<MockTest> GetTests(string category)
    {
        var name = Categories.FirstOrDefault(x => x.Name.Equals(category, StringComparison.OrdinalIgnoreCase))?.Name ?? category;
        return
        [
            new(1, name, $"{name} Full Mock Test 01", 200, 200, 60, "Latest Pattern"),
            new(2, name, $"{name} Full Mock Test 02", 200, 200, 60, ""),
            new(3, name, $"{name} Previous Year Paper (2023)", 200, 200, 60, "PYQ"),
            new(4, name, $"{name} Sectional Test - Quant", 50, 50, 30, ""),
            new(5, name, $"{name} Sectional Test - Reasoning", 50, 50, 30, ""),
            new(6, name, $"{name} Sectional Test - English", 50, 50, 30, "")
        ];
    }

    public static IReadOnlyList<TestQuestion> GetQuestions(int testId)
    {
        var seed = new[]
        {
            new TestQuestion(1, testId, "Reasoning", "Which number will replace the question mark (?) in the series?", "2, 6, 12, 20, 30, ?", ["40", "42", "44", "46"], "42", "The differences are 4, 6, 8, 10, so the next difference is 12. 30 + 12 = 42."),
            new TestQuestion(2, testId, "Reasoning", "If CAT is coded as DBU, how is DOG coded?", null, ["EPH", "EOG", "FPH", "DPH"], "EPH", "Each letter is shifted one position forward."),
            new TestQuestion(3, testId, "Quantitative Aptitude", "What is 25% of 240?", null, ["50", "60", "70", "80"], "60", "25% is one quarter. 240 / 4 = 60."),
            new TestQuestion(4, testId, "Quantitative Aptitude", "A train travels 120 km in 2 hours. What is its average speed?", null, ["40 km/h", "50 km/h", "60 km/h", "80 km/h"], "60 km/h", "Speed = distance / time = 120 / 2 = 60 km/h."),
            new TestQuestion(5, testId, "General Awareness", "Which is the largest planet in our solar system?", null, ["Earth", "Mars", "Jupiter", "Saturn"], "Jupiter", "Jupiter is the largest planet in the solar system."),
            new TestQuestion(6, testId, "General Awareness", "The Constitution of India came into effect on which date?", null, ["15 August 1947", "26 January 1950", "26 November 1949", "2 October 1950"], "26 January 1950", "The Constitution was adopted on 26 November 1949 and came into force on 26 January 1950."),
            new TestQuestion(7, testId, "English", "Choose the synonym of 'Rapid'.", null, ["Slow", "Quick", "Weak", "Late"], "Quick", "Rapid means quick or fast."),
            new TestQuestion(8, testId, "English", "Choose the correctly spelled word.", null, ["Accomodation", "Accommodation", "Acommodation", "Accommadation"], "Accommodation", "Accommodation is the correct spelling."),
        };

        return seed;
    }
}
