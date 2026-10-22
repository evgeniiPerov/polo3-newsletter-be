-- Target database: NewsletterTest (SQL Server 2019+)
-- Creates table dbo.NewsletterSubscription, its indexes, three stored procedures and two seed rows.
-- Safe to run repeatedly: existing objects are kept, procedures are replaced, seed rows are inserted once.
-- Status values: 1 = pending confirmation, 2 = active, 3 = unsubscribed.

USE [NewsletterTest];
GO

-- Filtered indexes require these options both when created and when procedures that touch the table are compiled.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.NewsletterSubscription', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NewsletterSubscription
    (
        IdNewsletterSubscription int IDENTITY(1, 1) NOT NULL,
        -- Case-insensitive collation on the column itself, so uniqueness does not depend on the database default.
        Email nvarchar(254) COLLATE Latin1_General_100_CI_AS NOT NULL,
        Name nvarchar(100) NULL,
        IsConsent bit NOT NULL,
        Status tinyint NOT NULL,
        ConfirmToken varchar(64) NULL,
        UnsubscribeToken varchar(64) NOT NULL,
        DateCreated datetime2(0) NOT NULL CONSTRAINT DF_NewsletterSubscription_DateCreated DEFAULT (SYSUTCDATETIME()),
        DateConfirmed datetime2(0) NULL,
        DateUnsubscribed datetime2(0) NULL,
        CONSTRAINT PK_NewsletterSubscription PRIMARY KEY CLUSTERED (IdNewsletterSubscription),
        CONSTRAINT CK_NewsletterSubscription_Status CHECK (Status IN (1, 2, 3))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_NewsletterSubscription_Email' AND object_id = OBJECT_ID(N'dbo.NewsletterSubscription'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_NewsletterSubscription_Email ON dbo.NewsletterSubscription (Email);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_NewsletterSubscription_ConfirmToken' AND object_id = OBJECT_ID(N'dbo.NewsletterSubscription'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_NewsletterSubscription_ConfirmToken ON dbo.NewsletterSubscription (ConfirmToken)
        WHERE ConfirmToken IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_NewsletterSubscription_UnsubscribeToken' AND object_id = OBJECT_ID(N'dbo.NewsletterSubscription'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_NewsletterSubscription_UnsubscribeToken ON dbo.NewsletterSubscription (UnsubscribeToken);
GO

-- Registers an email or refreshes a pending/unsubscribed registration; an active subscription is left unchanged.
-- Returns one row: Status, ConfirmToken (NULL when already active), UnsubscribeToken, IsNew.
CREATE OR ALTER PROCEDURE dbo.SyncNewsletterSubscriptionData
    @email nvarchar(254),
    @name nvarchar(100),
    @isConsent bit,
    @confirmToken varchar(64),
    @unsubscribeToken varchar(64)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @statusPending tinyint = 1;
    DECLARE @statusUnsubscribed tinyint = 3;
    DECLARE @idNewsletterSubscription int;
    DECLARE @status tinyint;
    DECLARE @isNew bit = 0;

    BEGIN TRANSACTION;

    -- UPDLOCK + HOLDLOCK keep the email's key range locked until commit, so two concurrent
    -- first registrations of the same email cannot both see "no row" and both insert.
    SELECT @idNewsletterSubscription = IdNewsletterSubscription, @status = Status
    FROM dbo.NewsletterSubscription WITH (UPDLOCK, HOLDLOCK)
    WHERE Email = @email;

    IF @idNewsletterSubscription IS NULL
    BEGIN
        INSERT INTO dbo.NewsletterSubscription (Email, Name, IsConsent, Status, ConfirmToken, UnsubscribeToken)
        VALUES (@email, @name, @isConsent, @statusPending, @confirmToken, @unsubscribeToken);

        SET @idNewsletterSubscription = SCOPE_IDENTITY();
        SET @isNew = 1;
    END
    ELSE IF @status = @statusPending
    BEGIN
        -- The existing confirm token is kept, so the link from the first email still works and is sent again.
        UPDATE dbo.NewsletterSubscription
        SET Name = @name, IsConsent = @isConsent
        WHERE IdNewsletterSubscription = @idNewsletterSubscription;
    END
    ELSE IF @status = @statusUnsubscribed
    BEGIN
        UPDATE dbo.NewsletterSubscription
        SET Name = @name,
            IsConsent = @isConsent,
            Status = @statusPending,
            ConfirmToken = @confirmToken,
            DateConfirmed = NULL,
            DateUnsubscribed = NULL
        WHERE IdNewsletterSubscription = @idNewsletterSubscription;
    END;

    COMMIT TRANSACTION;

    SELECT Status, ConfirmToken, UnsubscribeToken, @isNew AS IsNew
    FROM dbo.NewsletterSubscription
    WHERE IdNewsletterSubscription = @idNewsletterSubscription;
END;
GO

-- Activates a pending subscription and invalidates its confirm token.
-- Returns 1 when confirmed, 0 when the token is unknown or already used.
CREATE OR ALTER PROCEDURE dbo.ConfirmNewsletterSubscription
    @confirmToken varchar(64)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @statusPending tinyint = 1;
    DECLARE @statusActive tinyint = 2;

    UPDATE dbo.NewsletterSubscription
    SET Status = @statusActive, ConfirmToken = NULL, DateConfirmed = SYSUTCDATETIME()
    WHERE ConfirmToken = @confirmToken AND Status = @statusPending;

    SELECT @@ROWCOUNT AS ConfirmedCount;
END;
GO

-- Unsubscribes by token; a repeated call changes nothing.
-- Returns 0 when the token is unknown, 1 when unsubscribed now, 2 when already unsubscribed.
CREATE OR ALTER PROCEDURE dbo.UnsubscribeNewsletterSubscription
    @unsubscribeToken varchar(64)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @statusUnsubscribed tinyint = 3;

    UPDATE dbo.NewsletterSubscription
    SET Status = @statusUnsubscribed, ConfirmToken = NULL, DateUnsubscribed = SYSUTCDATETIME()
    WHERE UnsubscribeToken = @unsubscribeToken AND Status <> @statusUnsubscribed;

    IF @@ROWCOUNT = 1
        SELECT 1 AS UnsubscribeOutcome;
    ELSE IF EXISTS (SELECT 1 FROM dbo.NewsletterSubscription WHERE UnsubscribeToken = @unsubscribeToken)
        SELECT 2 AS UnsubscribeOutcome;
    ELSE
        SELECT 0 AS UnsubscribeOutcome;
END;
GO

-- Test data: one active and one unsubscribed subscription.
IF NOT EXISTS (SELECT 1 FROM dbo.NewsletterSubscription WHERE Email = N'seed.active@example.com')
    INSERT INTO dbo.NewsletterSubscription (Email, Name, IsConsent, Status, ConfirmToken, UnsubscribeToken, DateConfirmed)
    VALUES (N'seed.active@example.com', N'Seed Active', 1, 2, NULL, 'SeedActiveUnsubscribeToken00000000000000001', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.NewsletterSubscription WHERE Email = N'seed.unsubscribed@example.com')
    INSERT INTO dbo.NewsletterSubscription (Email, Name, IsConsent, Status, ConfirmToken, UnsubscribeToken, DateConfirmed, DateUnsubscribed)
    VALUES (N'seed.unsubscribed@example.com', N'Seed Unsubscribed', 1, 3, NULL, 'SeedUnsubscribedToken0000000000000000000002', SYSUTCDATETIME(), SYSUTCDATETIME());
GO
