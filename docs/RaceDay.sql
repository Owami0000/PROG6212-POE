---Create the Database---
CREATE DATABASE RaceDay;

USE RaceDay;

---Create Tables---

---Users Table (Stores login credentials and role)---
CREATE TABLE Users (
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    Email VARCHAR(255) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL, -- In a real app, this would be a hashed password
    Role VARCHAR(50) NOT NULL CHECK (Role IN ('Organiser', 'Participant')),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
);
SELECT * FROM Users;

---Profiles Table (Stores personal details of users)---
CREATE TABLE Profiles (
    ProfileID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NOT NULL UNIQUE FOREIGN KEY REFERENCES Users(UserID) ON DELETE CASCADE,
    FirstName VARCHAR(100) NOT NULL,
    LastName VARCHAR(100) NOT NULL,
    DateOfBirth DATE NOT NULL,
    Gender VARCHAR(10) CHECK (Gender IN ('Male', 'Female', 'Other', 'Prefer not to say')),
    PhoneNumber VARCHAR(20)
);
SELECT * FROM Profiles;

---Events Table (Stores details of each event)---
CREATE TABLE Events (
    EventID INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
    Name VARCHAR(255) NOT NULL,
    Description VARCHAR(MAX),
    [Date] DATETIME2 NOT NULL,
    Location VARCHAR(255) NOT NULL,
    Distance DECIMAL(5,2) NOT NULL, 
    EventType VARCHAR(50) NOT NULL CHECK (EventType IN ('Run', 'Walk', 'Cycle')),
    Status VARCHAR(50) DEFAULT 'Open' NOT NULL CHECK (Status IN ('Draft', 'Open', 'Closed', 'Archived')),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
);
SELECT * FROM Events;

---Categories Table (Defines categories for each event)---
CREATE TABLE Categories (
    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
    EventID INT NOT NULL FOREIGN KEY REFERENCES Events(EventID) ON DELETE CASCADE,
    Name VARCHAR(100) NOT NULL, 
    MinAge INT, 
    MaxAge INT, 
    Distance DECIMAL(5,2) 
);
SELECT * FROM Categories;

---Enrolments Table (Links a Participant to a specific Event and Category)---
CREATE TABLE Enrolments (
    EnrolmentID INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
    CategoryID INT NOT NULL FOREIGN KEY REFERENCES Categories(CategoryID),
    EnrolmentDate DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
    Status VARCHAR(50) DEFAULT 'Confirmed' NOT NULL CHECK (Status IN ('Confirmed', 'Cancelled', 'Waiting List'))
);
SELECT * FROM Enrolments;

---Results Table (Stores the performance of a participant for an enrolment)---
CREATE TABLE Results (
    ResultID INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID INT NOT NULL UNIQUE FOREIGN KEY REFERENCES Enrolments(EnrolmentID) ON DELETE CASCADE,
    FinishTime TIME(0) NOT NULL, 
    Position INT NOT NULL
);
SELECT * FROM Results;


---Insert Organisers---
INSERT INTO Users (Email, PasswordHash, Role) VALUES
('thabo.mokoena@raceday.co.za', 'hashed_password_123', 'Organiser'),
('lindiwe.ntuli@raceday.co.za', 'hashed_password_456', 'Organiser');

---Insert Participants---
INSERT INTO Users (Email, PasswordHash, Role) VALUES
('siya.kolisi@example.com', 'hashed_password_789', 'Participant'),
('caster.semenya@example.com', 'hashed_password_101', 'Participant');

---Insert Profiles for Organisers---
INSERT INTO Profiles (UserID, FirstName, LastName, DateOfBirth, Gender, PhoneNumber) VALUES
(1, 'Thabo', 'Mokoena', '1985-03-15', 'Male', '+27 82 123 4567'),
(2, 'Lindiwe', 'Ntuli', '1990-07-22', 'Female', '+27 73 987 6543');

---Insert Profiles for Participants---
INSERT INTO Profiles (UserID, FirstName, LastName, DateOfBirth, Gender, PhoneNumber) VALUES
(3, 'Siya', 'Kolisi', '1991-06-16', 'Male', '+27 71 555 7890'),
(4, 'Caster', 'Semenya', '1991-01-07', 'Female', '+27 81 444 3210');

---Insert Events---
INSERT INTO Events (OrganiserID, Name, Description, [Date], Location, Distance, EventType, Status) VALUES
(1, 'Soweto Marathon 2026', 'The iconic Soweto Marathon through the streets of Soweto.', '2026-11-01 06:00:00', 'Soweto, Johannesburg', 42.20, 'Run', 'Open'),
(1, '5km Park Run - Zoo Lake', 'A community 5km fun run around Zoo Lake.', '2026-10-15 08:00:00', 'Zoo Lake, Johannesburg', 5.00, 'Run', 'Open'),
(2, 'Cape Town Cycle Tour', 'The world''s largest timed cycle race.', '2026-03-08 08:00:00', 'Cape Town', 109.00, 'Cycle', 'Open');

---Insert Categories for Events (Soweto Marathon)---
INSERT INTO Categories (EventID, Name, MinAge, MaxAge) VALUES
(1, 'Junior (Under 20)', 13, 19),
(1, 'Senior (20-39)', 20, 39),
(1, 'Veteran (40-49)', 40, 49),
(1, 'Master (50+)', 50, NULL);

---Insert Categories for Events (5km Park Run)---
INSERT INTO Categories (EventID, Name, MinAge, MaxAge) VALUES
(2, 'All Ages', NULL, NULL), -- No age restrictions
(2, 'Under 13', 5, 12);

---Insert Categories for Events (Cape Town Cycle Tour)---
INSERT INTO Categories (EventID, Name, MinAge, MaxAge) VALUES
(3, 'Elite', 18, 39),
(3, 'Veteran', 40, 60),
(3, 'Grand Master', 61, NULL);

---Insert Sample Enrolments---
INSERT INTO Enrolments (ParticipantID, CategoryID, Status) VALUES
(3, 2, 'Confirmed'), -- Siya Kolisi enrolled in 'Senior' category for Soweto Marathon
(4, 5, 'Confirmed'), -- Caster Semenya enrolled in 'All Ages' category for 5km Park Run
(3, 7, 'Confirmed'); -- Siya Kolisi enrolled in 'Elite' category for Cape Town Cycle Tour

-- Insert Sample Results
-- Siya finishes the Soweto Marathon in 3:45:23 at position 345
INSERT INTO Results (EnrolmentID, FinishTime, Position) VALUES (1, '03:45:23', 345);

-- Caster finishes the 5km Park Run in 0:22:15 at position 12
INSERT INTO Results (EnrolmentID, FinishTime, Position) VALUES (2, '00:22:15', 12);

---EnrolmentID 3 (Cape Town Cycle Tour) has no result yet, as the event hasn't happened.


---Verify the Data---
SELECT * FROM Users;
SELECT * FROM Profiles;
SELECT * FROM Events;
SELECT * FROM Categories;
SELECT * FROM Enrolments;
SELECT * FROM Results; 