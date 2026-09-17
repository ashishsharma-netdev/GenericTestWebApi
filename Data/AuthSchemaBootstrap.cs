using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Data;

public static class AuthSchemaBootstrap
{
    public static async Task EnsureAsync(TestPrepDbContext db)
    {
        // Keep schema creation and index creation in separate SQL batches.
        // SQL Server compiles a batch before executing it, so an ALTER TABLE that
        // adds GoogleSubject cannot be safely followed by an index referencing
        // that newly-added column in the same batch.
        var schemaSql = @"
IF OBJECT_ID('dbo.Users','U') IS NULL CREATE TABLE dbo.Users (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, FullName nvarchar(150) NOT NULL, Email nvarchar(256) NOT NULL, PasswordHash nvarchar(max) NOT NULL, GoogleSubject nvarchar(255) NULL, IsActive bit NOT NULL DEFAULT 1, CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF COL_LENGTH('dbo.Users','GoogleSubject') IS NULL ALTER TABLE dbo.Users ADD GoogleSubject nvarchar(255) NULL;
IF OBJECT_ID('dbo.Roles','U') IS NULL CREATE TABLE dbo.Roles (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, Name nvarchar(50) NOT NULL);
IF OBJECT_ID('dbo.UserRoles','U') IS NULL CREATE TABLE dbo.UserRoles (UserId int NOT NULL, RoleId int NOT NULL, CONSTRAINT PK_UserRoles PRIMARY KEY(UserId,RoleId), CONSTRAINT FK_UserRoles_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE, CONSTRAINT FK_UserRoles_Roles FOREIGN KEY(RoleId) REFERENCES dbo.Roles(Id) ON DELETE CASCADE);
IF OBJECT_ID('dbo.RefreshTokens','U') IS NULL CREATE TABLE dbo.RefreshTokens (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, UserId int NOT NULL, TokenHash nvarchar(128) NOT NULL, ExpiresAtUtc datetime2 NOT NULL, RevokedAtUtc datetime2 NULL, CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE);
IF OBJECT_ID('dbo.TestAttempts','U') IS NULL CREATE TABLE dbo.TestAttempts (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, UserId int NOT NULL, MockTestId int NOT NULL, StartedAtUtc datetime2 NOT NULL, SubmittedAtUtc datetime2 NOT NULL, TimeTakenSeconds int NOT NULL, Correct int NOT NULL, Incorrect int NOT NULL, Unattempted int NOT NULL, Score decimal(10,2) NOT NULL, Percentage decimal(6,2) NOT NULL, CONSTRAINT FK_TestAttempts_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE, CONSTRAINT FK_TestAttempts_MockTests FOREIGN KEY(MockTestId) REFERENCES dbo.MockTests(Id));
IF OBJECT_ID('dbo.TestAttemptAnswers','U') IS NULL CREATE TABLE dbo.TestAttemptAnswers (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, TestAttemptId int NOT NULL, QuestionId int NOT NULL, Answer nvarchar(20) NULL, IsCorrect bit NOT NULL, MarkedForReview bit NOT NULL, CONSTRAINT FK_TestAttemptAnswers_Attempts FOREIGN KEY(TestAttemptId) REFERENCES dbo.TestAttempts(Id) ON DELETE CASCADE, CONSTRAINT FK_TestAttemptAnswers_Questions FOREIGN KEY(QuestionId) REFERENCES dbo.TestQuestions(Id));
IF OBJECT_ID('dbo.SubscriptionPlans','U') IS NULL CREATE TABLE dbo.SubscriptionPlans (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, Name nvarchar(100) NOT NULL, Code nvarchar(30) NOT NULL, Price decimal(10,2) NOT NULL, Currency nvarchar(10) NOT NULL, DurationDays int NOT NULL, DisplayOrder int NOT NULL, IsActive bit NOT NULL DEFAULT 1);
IF OBJECT_ID('dbo.Subscriptions','U') IS NULL CREATE TABLE dbo.Subscriptions (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, UserId int NOT NULL, PlanId int NOT NULL, Provider nvarchar(30) NOT NULL, ProviderOrderId nvarchar(100) NULL, ProviderPaymentId nvarchar(100) NULL, Status nvarchar(30) NOT NULL, StartedAtUtc datetime2 NULL, ExpiresAtUtc datetime2 NULL, CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT FK_Subscriptions_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE, CONSTRAINT FK_Subscriptions_Plans FOREIGN KEY(PlanId) REFERENCES dbo.SubscriptionPlans(Id));
IF OBJECT_ID('dbo.Payments','U') IS NULL CREATE TABLE dbo.Payments (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, UserId int NOT NULL, SubscriptionId int NULL, Provider nvarchar(30) NOT NULL, ProviderOrderId nvarchar(100) NOT NULL, ProviderPaymentId nvarchar(100) NULL, Amount decimal(10,2) NOT NULL, Currency nvarchar(10) NOT NULL, Status nvarchar(30) NOT NULL, CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), PaidAtUtc datetime2 NULL, CONSTRAINT FK_Payments_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION, CONSTRAINT FK_Payments_Subscriptions FOREIGN KEY(SubscriptionId) REFERENCES dbo.Subscriptions(Id) ON DELETE SET NULL);
IF OBJECT_ID('dbo.AdminAuditLogs','U') IS NULL CREATE TABLE dbo.AdminAuditLogs (Id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY, AdminUserId int NOT NULL, Action nvarchar(100) NOT NULL, TargetType nvarchar(50) NOT NULL, TargetId int NULL, Details nvarchar(2000) NULL, CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT FK_AdminAuditLogs_AdminUser FOREIGN KEY(AdminUserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION);";

        await db.Database.ExecuteSqlRawAsync(schemaSql);

        // This is intentionally a separate batch so the GoogleSubject column is
        // visible to SQL Server compilation when the filtered index is created.
        var indexAndSeedSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Users_Email' AND object_id=OBJECT_ID('dbo.Users')) CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users(Email);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Users_GoogleSubject' AND object_id=OBJECT_ID('dbo.Users')) CREATE UNIQUE INDEX UX_Users_GoogleSubject ON dbo.Users(GoogleSubject) WHERE GoogleSubject IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Roles_Name' AND object_id=OBJECT_ID('dbo.Roles')) CREATE UNIQUE INDEX UX_Roles_Name ON dbo.Roles(Name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_RefreshTokens_Hash' AND object_id=OBJECT_ID('dbo.RefreshTokens')) CREATE UNIQUE INDEX UX_RefreshTokens_Hash ON dbo.RefreshTokens(TokenHash);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_AttemptAnswers' AND object_id=OBJECT_ID('dbo.TestAttemptAnswers')) CREATE UNIQUE INDEX UX_AttemptAnswers ON dbo.TestAttemptAnswers(TestAttemptId,QuestionId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_SubscriptionPlans_Code' AND object_id=OBJECT_ID('dbo.SubscriptionPlans')) CREATE UNIQUE INDEX UX_SubscriptionPlans_Code ON dbo.SubscriptionPlans(Code);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Subscriptions_Order' AND object_id=OBJECT_ID('dbo.Subscriptions')) CREATE UNIQUE INDEX UX_Subscriptions_Order ON dbo.Subscriptions(ProviderOrderId) WHERE ProviderOrderId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Payments_Payment' AND object_id=OBJECT_ID('dbo.Payments')) CREATE UNIQUE INDEX UX_Payments_Payment ON dbo.Payments(ProviderPaymentId) WHERE ProviderPaymentId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_AdminAuditLogs_CreatedAtUtc' AND object_id=OBJECT_ID('dbo.AdminAuditLogs')) CREATE INDEX IX_AdminAuditLogs_CreatedAtUtc ON dbo.AdminAuditLogs(CreatedAtUtc);
UPDATE dbo.MockTests SET IsFree = CASE WHEN Title LIKE '%Full Mock Test 01' OR Title LIKE '%Full Mock Test 02' THEN 1 ELSE 0 END WHERE Tag = 'Latest Pattern' OR Title LIKE '%Full Mock Test 02' OR Title LIKE '%Previous Year Paper (2023)' OR Title LIKE '%Sectional Test - %';";

        await db.Database.ExecuteSqlRawAsync(indexAndSeedSql);
    }
}
