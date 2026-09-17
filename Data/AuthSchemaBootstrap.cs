using Microsoft.EntityFrameworkCore;

namespace GenericTestWebApi.Data;

public static class AuthSchemaBootstrap
{
    public static async Task EnsureAsync(TestPrepDbContext db)
    {
        var sql = @"
IF OBJECT_ID('dbo.Users','U') IS NULL CREATE TABLE dbo.Users (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, FullName nvarchar(150) NOT NULL, Email nvarchar(256) NOT NULL, PasswordHash nvarchar(max) NOT NULL, GoogleSubject nvarchar(255) NULL, IsActive bit NOT NULL DEFAULT 1, CreatedAtUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME());
IF COL_LENGTH('dbo.Users','GoogleSubject') IS NULL ALTER TABLE dbo.Users ADD GoogleSubject nvarchar(255) NULL;
IF OBJECT_ID('dbo.Roles','U') IS NULL CREATE TABLE dbo.Roles (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, Name nvarchar(50) NOT NULL);
IF OBJECT_ID('dbo.UserRoles','U') IS NULL CREATE TABLE dbo.UserRoles (UserId int NOT NULL, RoleId int NOT NULL, CONSTRAINT PK_UserRoles PRIMARY KEY(UserId,RoleId), CONSTRAINT FK_UserRoles_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE, CONSTRAINT FK_UserRoles_Roles FOREIGN KEY(RoleId) REFERENCES dbo.Roles(Id) ON DELETE CASCADE);
IF OBJECT_ID('dbo.RefreshTokens','U') IS NULL CREATE TABLE dbo.RefreshTokens (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, UserId int NOT NULL, TokenHash nvarchar(128) NOT NULL, ExpiresAtUtc datetime2 NOT NULL, RevokedAtUtc datetime2 NULL, CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE);
IF OBJECT_ID('dbo.TestAttempts','U') IS NULL CREATE TABLE dbo.TestAttempts (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, UserId int NOT NULL, MockTestId int NOT NULL, StartedAtUtc datetime2 NOT NULL, SubmittedAtUtc datetime2 NOT NULL, TimeTakenSeconds int NOT NULL, Correct int NOT NULL, Incorrect int NOT NULL, Unattempted int NOT NULL, Score decimal(10,2) NOT NULL, Percentage decimal(6,2) NOT NULL, CONSTRAINT FK_TestAttempts_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE, CONSTRAINT FK_TestAttempts_MockTests FOREIGN KEY(MockTestId) REFERENCES dbo.MockTests(Id));
IF OBJECT_ID('dbo.TestAttemptAnswers','U') IS NULL CREATE TABLE dbo.TestAttemptAnswers (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, TestAttemptId int NOT NULL, QuestionId int NOT NULL, Answer nvarchar(20) NULL, IsCorrect bit NOT NULL, MarkedForReview bit NOT NULL, CONSTRAINT FK_TestAttemptAnswers_Attempts FOREIGN KEY(TestAttemptId) REFERENCES dbo.TestAttempts(Id) ON DELETE CASCADE, CONSTRAINT FK_TestAttemptAnswers_Questions FOREIGN KEY(QuestionId) REFERENCES dbo.TestQuestions(Id));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Users_Email' AND object_id=OBJECT_ID('dbo.Users')) CREATE UNIQUE INDEX UX_Users_Email ON dbo.Users(Email);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Users_GoogleSubject' AND object_id=OBJECT_ID('dbo.Users')) CREATE UNIQUE INDEX UX_Users_GoogleSubject ON dbo.Users(GoogleSubject) WHERE GoogleSubject IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Roles_Name' AND object_id=OBJECT_ID('dbo.Roles')) CREATE UNIQUE INDEX UX_Roles_Name ON dbo.Roles(Name);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_RefreshTokens_Hash' AND object_id=OBJECT_ID('dbo.RefreshTokens')) CREATE UNIQUE INDEX UX_RefreshTokens_Hash ON dbo.RefreshTokens(TokenHash);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_AttemptAnswers' AND object_id=OBJECT_ID('dbo.TestAttemptAnswers')) CREATE UNIQUE INDEX UX_AttemptAnswers ON dbo.TestAttemptAnswers(TestAttemptId,QuestionId);";
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}