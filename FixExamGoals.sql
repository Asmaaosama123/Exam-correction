-- 1. Create ExamGoals table
CREATE TABLE [ExamGoals] (
    [Id] int NOT NULL IDENTITY(1, 1),
    [ExamId] int NOT NULL,
    [GoalText] nvarchar(max) NOT NULL,
    [QuestionNumbers] nvarchar(max) NOT NULL,
    [OwnerId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_ExamGoals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ExamGoals_AspNetUsers_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ExamGoals_Exams_ExamId] FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE CASCADE
);
GO

-- 2. Create Indexes
CREATE INDEX [IX_ExamGoals_ExamId] ON [ExamGoals] ([ExamId]);
GO
CREATE INDEX [IX_ExamGoals_OwnerId] ON [ExamGoals] ([OwnerId]);
GO

-- 3. Mark migration as applied (optional but recommended)
-- Replace '20260308214616_AddExamGoals' if your migration history name is different
IF EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260308214616_AddExamGoals', '10.0.0');
END
GO
