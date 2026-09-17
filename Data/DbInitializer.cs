using GenericTestWebApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(TestPrepDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.ExamCategories.AnyAsync()) return;

        var categories = new[]
        {
            new ExamCategoryEntity { Name="SSC", Description="CGL, CHSL, MTS, GD, Stenographer", Icon="◉", Accent="#e33434" },
            new ExamCategoryEntity { Name="Railway", Description="RRB NTPC, Group D, ALP, JE", Icon="◆", Accent="#18a957" },
            new ExamCategoryEntity { Name="Bank", Description="IBPS, SBI, RRB, PO, Clerk", Icon="▥", Accent="#1877d2" },
            new ExamCategoryEntity { Name="State Exam", Description="PSC, Police, SI, TET and more", Icon="♜", Accent="#f07824" },
            new ExamCategoryEntity { Name="UPSC", Description="Civil Services (IAS/IPS/IFS)", Icon="♛", Accent="#6546d9" },
            new ExamCategoryEntity { Name="CAT", Description="Common Admission Test", Icon="∞", Accent="#db3b91" },
            new ExamCategoryEntity { Name="CTET", Description="Central Teacher Eligibility Test", Icon="◌", Accent="#12a9bb" }
        };
        db.ExamCategories.AddRange(categories);
        await db.SaveChangesAsync();

        foreach (var category in categories)
        {
            var test = new MockTestEntity
            {
                ExamCategoryId = category.Id, Title = $"{category.Name} Full Mock Test 01",
                TotalQuestions = 8, TotalMarks = 8, DurationMinutes = 60, NegativeMarking = .5m, Tag = "Latest Pattern"
            };
            db.MockTests.Add(test);
            await db.SaveChangesAsync();
            db.TestQuestions.AddRange(GetQuestions(test.Id));
            db.MockTests.Add(new MockTestEntity { ExamCategoryId=category.Id, Title=$"{category.Name} Full Mock Test 02", TotalQuestions=8, TotalMarks=8, DurationMinutes=60, NegativeMarking=.5m });
            db.MockTests.Add(new MockTestEntity { ExamCategoryId=category.Id, Title=$"{category.Name} Previous Year Paper (2023)", TotalQuestions=8, TotalMarks=8, DurationMinutes=60, NegativeMarking=.5m, Tag="PYQ" });
            db.MockTests.Add(new MockTestEntity { ExamCategoryId=category.Id, Title=$"{category.Name} Sectional Test - Quant", TotalQuestions=2, TotalMarks=2, DurationMinutes=30, NegativeMarking=.5m });
            db.MockTests.Add(new MockTestEntity { ExamCategoryId=category.Id, Title=$"{category.Name} Sectional Test - Reasoning", TotalQuestions=2, TotalMarks=2, DurationMinutes=30, NegativeMarking=.5m });
            db.MockTests.Add(new MockTestEntity { ExamCategoryId=category.Id, Title=$"{category.Name} Sectional Test - English", TotalQuestions=2, TotalMarks=2, DurationMinutes=30, NegativeMarking=.5m });
        }
        await db.SaveChangesAsync();
    }

    private static IEnumerable<TestQuestionEntity> GetQuestions(int testId) => new[]
    {
        new TestQuestionEntity { MockTestId=testId, DisplayOrder=1, Section="Reasoning", Text="Which number will replace the question mark (?) in the series?", Series="2, 6, 12, 20, 30, ?", OptionA="40", OptionB="42", OptionC="44", OptionD="46", CorrectAnswer="B", Explanation="Differences are 4, 6, 8, 10, so the next difference is 12. 30 + 12 = 42.", Difficulty="Easy" },
        new TestQuestionEntity { MockTestId=testId, DisplayOrder=2, Section="Reasoning", Text="If CAT is coded as DBU, how is DOG coded?", OptionA="EPH", OptionB="EOG", OptionC="FPH", OptionD="DPH", CorrectAnswer="A", Explanation="Each letter is shifted one position forward.", Difficulty="Easy" },
        new TestQuestionEntity { MockTestId=testId, DisplayOrder=3, Section="Quantitative Aptitude", Text="What is 25% of 240?", OptionA="50", OptionB="60", OptionC="70", OptionD="80", CorrectAnswer="B", Explanation="25% is one quarter. 240 / 4 = 60.", Difficulty="Easy" },
        new TestQuestionEntity { MockTestId=testId, DisplayOrder=4, Section="Quantitative Aptitude", Text="A train travels 120 km in 2 hours. What is its average speed?", OptionA="40 km/h", OptionB="50 km/h", OptionC="60 km/h", OptionD="80 km/h", CorrectAnswer="C", Explanation="Speed = distance / time = 120 / 2 = 60 km/h.", Difficulty="Easy" },
        new TestQuestionEntity { MockTestId=testId, DisplayOrder=5, Section="General Awareness", Text="Which is the largest planet in our solar system?", OptionA="Earth", OptionB="Mars", OptionC="Jupiter", OptionD="Saturn", CorrectAnswer="C", Explanation="Jupiter is the largest planet in the solar system.", Difficulty="Easy" },
        new TestQuestionEntity { MockTestId=testId, DisplayOrder=6, Section="General Awareness", Text="The Constitution of India came into effect on which date?", OptionA="15 August 1947", OptionB="26 January 1950", OptionC="26 November 1949", OptionD="2 October 1950", CorrectAnswer="B", Explanation="It came into force on 26 January 1950.", Difficulty="Medium" },
        new TestQuestionEntity { MockTestId=testId, DisplayOrder=7, Section="English", Text="Choose the synonym of 'Rapid'.", OptionA="Slow", OptionB="Quick", OptionC="Weak", OptionD="Late", CorrectAnswer="B", Explanation="Rapid means quick or fast.", Difficulty="Easy" },
        new TestQuestionEntity { MockTestId=testId, DisplayOrder=8, Section="English", Text="Choose the correctly spelled word.", OptionA="Accomodation", OptionB="Accommodation", OptionC="Acommodation", OptionD="Accommadation", CorrectAnswer="B", Explanation="Accommodation is the correct spelling.", Difficulty="Easy" }
    };
}