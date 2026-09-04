# PROG6212-POE
Part1 submission

## System Overview

**RaceDay** is a full-stack web-based event management system designed specifically for the South African road running, walking, and cycling community. The platform streamlines the entire event lifecycle - from creation and registration to result capturing - replacing manual paper-based processes with a modern digital solution.

### Core Purpose
- Enable **Event Organisers** to create and manage events efficiently
- Allow **Participants** to discover, enter, and track their performance in events
- Provide a centralised platform for South Africa's vibrant road events culture

## 👥 User Roles

### 1. Organiser
The **Organiser** is the event creator and administrator. They have full control over their events:

### 2. Participant

The **Participant** is the athlete/entrant who takes part in events


## Entity Relationship Diagram (ERD)

### Entities Overview

The database consists of **6 core entities** with a bridge entity pattern:

### Entity Descriptions

#### 1. Users
Stores login credentials and authentication information. Each user has a unique email and assigned role.

**Key Attributes:**
- UserID (Primary Key)
- Email (Unique)
- PasswordHash
- Role (Organiser or Participant)
- CreatedAt

#### 2. Profiles
Stores personal information about users. Separate from Users for better data management.

**Key Attributes:**
- ProfileID (Primary Key)
- UserID (Foreign Key, Unique)
- FirstName, LastName
- DateOfBirth, Gender
- PhoneNumber
- ProfilePicture

#### 3. Events
Stores all event information. Each event is created by an Organiser.

**Key Attributes:**
- EventID (Primary Key)
- OrganiserID (Foreign Key)
- Name, Description
- Date, Location
- Distance, EventType
- Status, CreatedAt

#### 4. Categories
Defines age groups or distance categories for each event.

**Key Attributes:**
- CategoryID (Primary Key)
- EventID (Foreign Key)
- Name
- MinAge, MaxAge
- Distance

#### 5. Enrolments (Bridge Entity)
Links Participants to Categories. Resolves the many-to-many relationship and stores enrolment data.

**Key Attributes:**
- EnrolmentID (Primary Key)
- ParticipantID (Foreign Key)
- CategoryID (Foreign Key)
- EnrolmentDate
- Status

#### 6. Results
Stores participant performance data for their enrolment.

### Why Enrolment is a Bridge Entity

#### The Problem: Many-to-Many Relationship
Without a bridge entity, we would have a direct many-to-many relationship between Participants and Categories:

**Key Attributes:**
- ResultID (Primary Key)
- EnrolmentID (Foreign Key, Unique)
- FinishTime
- Position

## API Endpoint Plan


  
