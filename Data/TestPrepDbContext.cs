using GenericTestWebApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Data;

public class TestPrepDbContext(DbContextOptions<TestPrepDbContext> options) : DbContext(options)
{
    public DbSet<ExamCategoryEntity> ExamCategories => Set<ExamCategoryEntity>();
    public DbSet<MockTestEntity> MockTests => Set<MockTestEntity>();
    public DbSet<TestQuestionEntity> TestQuestions => Set<TestQuestionEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<TestAttemptEntity> TestAttempts => Set<TestAttemptEntity>();
    public DbSet<TestAttemptAnswerEntity> TestAttemptAnswers => Set<TestAttemptAnswerEntity>();
    public DbSet<SubscriptionPlanEntity> SubscriptionPlans => Set<SubscriptionPlanEntity>();
    public DbSet<SubscriptionEntity> Subscriptions => Set<SubscriptionEntity>();
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExamCategoryEntity>(e=>{e.ToTable("ExamCategories");e.HasKey(x=>x.Id);e.Property(x=>x.Name).HasMaxLength(100).IsRequired();e.Property(x=>x.Description).HasMaxLength(300).IsRequired();e.Property(x=>x.Icon).HasMaxLength(20);e.Property(x=>x.Accent).HasMaxLength(20);e.HasIndex(x=>x.Name).IsUnique();});
        modelBuilder.Entity<MockTestEntity>(e=>{e.ToTable("MockTests");e.HasKey(x=>x.Id);e.Property(x=>x.Title).HasMaxLength(200).IsRequired();e.Property(x=>x.NegativeMarking).HasPrecision(4,2);e.HasOne(x=>x.ExamCategory).WithMany(x=>x.Tests).HasForeignKey(x=>x.ExamCategoryId).OnDelete(DeleteBehavior.Cascade);e.HasIndex(x=>new{x.ExamCategoryId,x.Title}).IsUnique();});
        modelBuilder.Entity<TestQuestionEntity>(e=>{e.ToTable("TestQuestions");e.HasKey(x=>x.Id);e.Property(x=>x.Text).HasMaxLength(2000).IsRequired();e.Property(x=>x.Section).HasMaxLength(100).IsRequired();e.Property(x=>x.Difficulty).HasMaxLength(30).IsRequired();e.Property(x=>x.OptionA).HasMaxLength(500).IsRequired();e.Property(x=>x.OptionB).HasMaxLength(500).IsRequired();e.Property(x=>x.OptionC).HasMaxLength(500).IsRequired();e.Property(x=>x.OptionD).HasMaxLength(500).IsRequired();e.Property(x=>x.CorrectAnswer).HasMaxLength(1).IsRequired();e.HasOne(x=>x.MockTest).WithMany(x=>x.Questions).HasForeignKey(x=>x.MockTestId).OnDelete(DeleteBehavior.Cascade);e.HasIndex(x=>new{x.MockTestId,x.DisplayOrder}).IsUnique();});
        modelBuilder.Entity<UserEntity>(e=>{e.ToTable("Users");e.HasKey(x=>x.Id);e.Property(x=>x.FullName).HasMaxLength(150).IsRequired();e.Property(x=>x.Email).HasMaxLength(256).IsRequired();e.Property(x=>x.PasswordHash).IsRequired();e.HasIndex(x=>x.Email).IsUnique();e.HasIndex(x=>x.GoogleSubject).IsUnique().HasFilter("[GoogleSubject] IS NOT NULL");});
        modelBuilder.Entity<RoleEntity>(e=>{e.ToTable("Roles");e.HasKey(x=>x.Id);e.Property(x=>x.Name).HasMaxLength(50).IsRequired();e.HasIndex(x=>x.Name).IsUnique();});
        modelBuilder.Entity<UserRoleEntity>(e=>{e.ToTable("UserRoles");e.HasKey(x=>new{x.UserId,x.RoleId});e.HasOne(x=>x.User).WithMany(x=>x.UserRoles).HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);e.HasOne(x=>x.Role).WithMany(x=>x.UserRoles).HasForeignKey(x=>x.RoleId).OnDelete(DeleteBehavior.Cascade);});
        modelBuilder.Entity<RefreshTokenEntity>(e=>{e.ToTable("RefreshTokens");e.HasKey(x=>x.Id);e.HasIndex(x=>x.TokenHash).IsUnique();e.HasOne(x=>x.User).WithMany(x=>x.RefreshTokens).HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);});
        modelBuilder.Entity<TestAttemptEntity>(e=>{e.ToTable("TestAttempts");e.HasKey(x=>x.Id);e.Property(x=>x.Score).HasPrecision(10,2);e.Property(x=>x.Percentage).HasPrecision(6,2);e.HasOne(x=>x.User).WithMany(x=>x.TestAttempts).HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);e.HasOne(x=>x.MockTest).WithMany().HasForeignKey(x=>x.MockTestId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<TestAttemptAnswerEntity>(e=>{e.ToTable("TestAttemptAnswers");e.HasKey(x=>x.Id);e.HasOne(x=>x.TestAttempt).WithMany(x=>x.Answers).HasForeignKey(x=>x.TestAttemptId).OnDelete(DeleteBehavior.Cascade);e.HasOne(x=>x.Question).WithMany().HasForeignKey(x=>x.QuestionId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.TestAttemptId,x.QuestionId}).IsUnique();});
        modelBuilder.Entity<SubscriptionPlanEntity>(e=>{e.ToTable("SubscriptionPlans");e.HasKey(x=>x.Id);e.Property(x=>x.Name).HasMaxLength(100).IsRequired();e.Property(x=>x.Code).HasMaxLength(30).IsRequired();e.Property(x=>x.Price).HasPrecision(10,2);e.Property(x=>x.Currency).HasMaxLength(10).IsRequired();e.HasIndex(x=>x.Code).IsUnique();});
        modelBuilder.Entity<SubscriptionEntity>(e=>{e.ToTable("Subscriptions");e.HasKey(x=>x.Id);e.Property(x=>x.Provider).HasMaxLength(30).IsRequired();e.Property(x=>x.ProviderOrderId).HasMaxLength(100);e.Property(x=>x.ProviderPaymentId).HasMaxLength(100);e.Property(x=>x.Status).HasMaxLength(30).IsRequired();e.HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);e.HasOne(x=>x.Plan).WithMany().HasForeignKey(x=>x.PlanId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>x.ProviderOrderId).IsUnique().HasFilter("[ProviderOrderId] IS NOT NULL");});
        modelBuilder.Entity<PaymentEntity>(e=>{e.ToTable("Payments");e.HasKey(x=>x.Id);e.Property(x=>x.Provider).HasMaxLength(30).IsRequired();e.Property(x=>x.ProviderOrderId).HasMaxLength(100).IsRequired();e.Property(x=>x.ProviderPaymentId).HasMaxLength(100);e.Property(x=>x.Amount).HasPrecision(10,2);e.Property(x=>x.Currency).HasMaxLength(10).IsRequired();e.Property(x=>x.Status).HasMaxLength(30).IsRequired();e.HasOne(x=>x.User).WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);e.HasOne(x=>x.Subscription).WithMany(x=>x.Payments).HasForeignKey(x=>x.SubscriptionId).OnDelete(DeleteBehavior.SetNull);e.HasIndex(x=>x.ProviderOrderId);e.HasIndex(x=>x.ProviderPaymentId).IsUnique().HasFilter("[ProviderPaymentId] IS NOT NULL");});
    }
}