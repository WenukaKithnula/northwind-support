/* =====================================================================
   Support Ticket Management System — reference schema
   ---------------------------------------------------------------------
   This script is the STARTER schema, kept so the database can be inspected or
   rebuilt without EF Core. It is not updated for the assignment: what the tasks
   added comes from EF Core migrations in
   apps/api/src/SupportDesk.Infrastructure/Data/Migrations, not from here:
     - AddTicketEscalations: the TicketEscalations table and its index
     - AddAgentPasswordHash: the nullable Agents.PasswordHash column
   Build the database with the API (or `npm run ef -- database update`) to get both.
   ===================================================================== */

IF DB_ID('SupportDesk') IS NULL
    CREATE DATABASE SupportDesk;
GO

USE SupportDesk;
GO

IF OBJECT_ID('dbo.AgentSpecializations', 'U') IS NOT NULL DROP TABLE dbo.AgentSpecializations;
IF OBJECT_ID('dbo.Tickets', 'U')              IS NOT NULL DROP TABLE dbo.Tickets;
IF OBJECT_ID('dbo.Agents', 'U')               IS NOT NULL DROP TABLE dbo.Agents;
IF OBJECT_ID('dbo.Customers', 'U')            IS NOT NULL DROP TABLE dbo.Customers;
IF OBJECT_ID('dbo.Categories', 'U')           IS NOT NULL DROP TABLE dbo.Categories;
GO

/* --------------------------------------------------------------- Categories */
CREATE TABLE dbo.Categories
(
    Id                 INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name               NVARCHAR(100) NOT NULL,
    IsActive           BIT           NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT (1),
    /* Handling rules kept as data rather than in code.                      */
    RequiresSpecialist BIT           NOT NULL CONSTRAINT DF_Categories_RequiresSpecialist DEFAULT (0),
    ForcesCriticalPriority BIT       NOT NULL CONSTRAINT DF_Categories_ForcesCritical DEFAULT (0),
    CONSTRAINT UQ_Categories_Name UNIQUE (Name)
);
GO

/* ---------------------------------------------------------------- Customers */
/* Tier: 1 = Standard, 2 = Premium */
CREATE TABLE dbo.Customers
(
    Id           INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    Name         NVARCHAR(200)  NOT NULL,
    Email        NVARCHAR(256)  NOT NULL,
    Phone        NVARCHAR(50)   NULL,
    Tier         INT            NOT NULL CONSTRAINT DF_Customers_Tier DEFAULT (1),
    CreatedAtUtc DATETIME2(3)   NOT NULL CONSTRAINT DF_Customers_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Customers_Email UNIQUE (Email),
    CONSTRAINT CK_Customers_Tier CHECK (Tier IN (1, 2))
);
GO

/* ------------------------------------------------------------------- Agents */
CREATE TABLE dbo.Agents
(
    Id             INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Agents PRIMARY KEY,
    FullName       NVARCHAR(200) NOT NULL,
    Email          NVARCHAR(256) NOT NULL,
    IsActive       BIT           NOT NULL CONSTRAINT DF_Agents_IsActive DEFAULT (1),
    MaxOpenTickets INT           NOT NULL CONSTRAINT DF_Agents_MaxOpenTickets DEFAULT (10),
    CreatedAtUtc   DATETIME2(3)  NOT NULL CONSTRAINT DF_Agents_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Agents_Email UNIQUE (Email),
    CONSTRAINT CK_Agents_MaxOpenTickets CHECK (MaxOpenTickets > 0)
);
GO

/* ------------------------------------------------- AgentSpecializations (M:N) */
CREATE TABLE dbo.AgentSpecializations
(
    AgentId    INT NOT NULL,
    CategoryId INT NOT NULL,
    CONSTRAINT PK_AgentSpecializations PRIMARY KEY (AgentId, CategoryId),
    CONSTRAINT FK_AgentSpecializations_Agents
        FOREIGN KEY (AgentId) REFERENCES dbo.Agents (Id) ON DELETE CASCADE,
    CONSTRAINT FK_AgentSpecializations_Categories
        FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_AgentSpecializations_CategoryId
    ON dbo.AgentSpecializations (CategoryId) INCLUDE (AgentId);
GO

/* ------------------------------------------------------------------ Tickets */
/* Priority: 1 Low, 2 Medium, 3 High, 4 Critical
   Status:   1 New, 2 Open, 3 InProgress, 4 Resolved, 5 Closed              */
CREATE TABLE dbo.Tickets
(
    Id              INT             IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tickets PRIMARY KEY,
    Reference       NVARCHAR(20)    NOT NULL,
    Title           NVARCHAR(200)   NOT NULL,
    Description     NVARCHAR(4000)  NOT NULL,
    CustomerId      INT             NOT NULL,
    CategoryId      INT             NOT NULL,
    AssignedAgentId INT             NULL,
    Priority        INT             NOT NULL CONSTRAINT DF_Tickets_Priority DEFAULT (2),
    Status          INT             NOT NULL CONSTRAINT DF_Tickets_Status DEFAULT (1),
    CreatedAtUtc    DATETIME2(3)    NOT NULL CONSTRAINT DF_Tickets_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc    DATETIME2(3)    NOT NULL CONSTRAINT DF_Tickets_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
    DueAtUtc        DATETIME2(3)    NULL,
    ResolvedAtUtc   DATETIME2(3)    NULL,
    CONSTRAINT UQ_Tickets_Reference UNIQUE (Reference),
    CONSTRAINT FK_Tickets_Customers FOREIGN KEY (CustomerId)  REFERENCES dbo.Customers (Id),
    CONSTRAINT FK_Tickets_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (Id),
    CONSTRAINT FK_Tickets_Agents FOREIGN KEY (AssignedAgentId) REFERENCES dbo.Agents (Id),
    CONSTRAINT CK_Tickets_Priority CHECK (Priority BETWEEN 1 AND 4),
    CONSTRAINT CK_Tickets_Status   CHECK (Status   BETWEEN 1 AND 5)
);
GO

/* Indexes that support the list screen's filters and sorts. */
CREATE INDEX IX_Tickets_Status_Priority   ON dbo.Tickets (Status, Priority) INCLUDE (DueAtUtc, AssignedAgentId);
CREATE INDEX IX_Tickets_CustomerId        ON dbo.Tickets (CustomerId);
CREATE INDEX IX_Tickets_AssignedAgentId   ON dbo.Tickets (AssignedAgentId) WHERE AssignedAgentId IS NOT NULL;
CREATE INDEX IX_Tickets_CategoryId        ON dbo.Tickets (CategoryId);
CREATE INDEX IX_Tickets_DueAtUtc          ON dbo.Tickets (DueAtUtc);
GO
