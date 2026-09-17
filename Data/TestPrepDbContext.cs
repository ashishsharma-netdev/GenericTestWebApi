using GenericTestWebApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Data;

public class TestPrepDbContext(DbContextOptions<TestPrepDbContext> options) : DbContext(options)
{
    public DbSet<ExamCategoryEntity> ExamCategories => Set<ExamCategoryEntity>();
    public DbSet<MockTestEntity> MockTests => Set<MockTestEntity>();
    public DbSet<TestQuestionEntity> TestQuestions => Set<TestQuestionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExamCategoryEntity>(e =>
        {
            e.ToTable("ExamCategories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Icon).HasMaxLength(20);
            e.Property(x => x.Accent).HasMaxLength(20);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<MockTestEntity>(e =>
        {
            e.ToTable("MockTests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.NegativeMarking).HasPrecision(4, 2);
            e.HasOne(x => x.ExamCategory).WithMany(x => x.Tests).HasForeignKey(x => x.ExamCategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ExamCategoryId, x.Title }).IsUnique();
        });

        modelBuilder.Entity<TestQuestionEntity>(e =>
        {
            e.ToTable("TestQuestions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Section).HasMaxLength(100).IsRequired();
            e.Property(x => x.Difficulty).HasMaxLength(30).IsRequired();
            e.Property(x => x.OptionA).HasMaxLength(500).IsRequired();
            e.Property(x => x.OptionB).HasMaxLength(500).IsRequired();
            e.Property(x => x.OptionC).HasMaxLength(500).IsRequired();
            e.Property(x => x.OptionD).HasMaxLength(500).IsRequired();
            e.Property(x => x.CorrectAnswer).HasMaxLength(1).IsRequired();
            e.HasOne(x => x.MockTest).WithMany(x => x.Questions).HasForeignKey(x => x.MockTestId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.MockTestId, x.DisplayOrder }).IsUnique();
        });
    }
}