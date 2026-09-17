namespace GenericTestWebApi.Entities;

public class UserEntity
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();
    public ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = new List<RefreshTokenEntity>();
    public ICollection<TestAttemptEntity> TestAttempts { get; set; } = new List<TestAttemptEntity>();
}

public class RoleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();
}

public class UserRoleEntity
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public UserEntity User { get; set; } = null!;
    public RoleEntity Role { get; set; } = null!;
}

public class RefreshTokenEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public UserEntity User { get; set; } = null!;
}

public class TestAttemptEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int MockTestId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public int TimeTakenSeconds { get; set; }
    public int Correct { get; set; }
    public int Incorrect { get; set; }
    public int Unattempted { get; set; }
    public decimal Score { get; set; }
    public decimal Percentage { get; set; }
    public UserEntity User { get; set; } = null!;
    public MockTestEntity MockTest { get; set; } = null!;
    public ICollection<TestAttemptAnswerEntity> Answers { get; set; } = new List<TestAttemptAnswerEntity>();
}

public class TestAttemptAnswerEntity
{
    public int Id { get; set; }
    public int TestAttemptId { get; set; }
    public int QuestionId { get; set; }
    public string? Answer { get; set; }
    public bool IsCorrect { get; set; }
    public bool MarkedForReview { get; set; }
    public TestAttemptEntity TestAttempt { get; set; } = null!;
    public TestQuestionEntity Question { get; set; } = null!;
}