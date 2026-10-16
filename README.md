# RaceDay

## Project Description
RaceDay is an event management system for running, walking and cycling events. Organisers create events and
categories and capture results; Participants browse events, enrol in a category and view their own results.
Part 2 delivers the RESTful API (ASP.NET Core Web API, .NET 8, EF Core Code First, SQL Server).

## User Roles
### Organiser
- Create, update and delete **their own** events (name, description, date, location, distance, type Run/Walk/Cycle)
- Create, update and delete categories for their events (e.g. Under 20, Senior, 10km)
- View all enrolments for their events and change enrolment status (Pending / Confirmed / Cancelled)
- Capture finish times and finishing positions
- View and update their own profile

### Participant
- Browse events and categories
- Enrol in an event by selecting a category
- View their own enrolments and their own results
- View and update their own profile

## Part 1
Planning documents (ERD, endpoint plan, SQL script) are in the [`docs`](docs) folder.

## Part 2
### Technologies Used
ASP.NET Core Web API (.NET 8), Entity Framework Core 8 (Code First), SQL Server, Swashbuckle (Swagger),
xUnit + `WebApplicationFactory` + EF Core InMemory (tests), GitHub Actions.

### Project Structure
```
RaceDay
├── docs/                    Part 1 ERD, endpoint plan, SQL script
├── RaceDay.Api/
│   ├── Controllers/         Auth, Profile, Events, Categories, Enrolments, Results
│   ├── Data/                RaceDayDbContext
│   ├── Filters/             SessionAuthorizeAttribute (401/403 role enforcement), SessionKeys
│   ├── Models/              Role, AppUser, Event, Category, Enrolment, Result
│   ├── Services/            PasswordHasher (PBKDF2), Mapping (model -> DTO)
│   └── Program.cs
├── RaceDay.Contracts/       Dtos.cs (shared request/response objects)
├── RaceDay.Api.Tests/       xUnit tests
├── .github/workflows/ci.yml
└── README.md
```

### Database Setup
1. Install .NET 8 SDK and SQL Server / SQL Express.
2. Edit `RaceDay.Api/appsettings.json` -> `ConnectionStrings:DefaultConnection` if your instance is not `.\SQLEXPRESS`.
3. In Visual Studio: **Tools > NuGet Package Manager > Package Manager Console**, default project `RaceDay.Api`:
   ```
   Add-Migration InitialRaceDayDatabase
   Update-Database
   ```
   (CLI alternative from the solution folder:
   `dotnet ef migrations add InitialRaceDayDatabase --project RaceDay.Api` then
   `dotnet ef database update --project RaceDay.Api`)
4. Open SSMS and confirm `RaceDayDb` contains Roles, Users, Events, Categories, Enrolments, Results.
   The two roles (Organiser, Participant) are seeded by the migration.

### How to Run the API
```
dotnet run --project RaceDay.Api
```
or press F5 in Visual Studio. Swagger opens at `http://localhost:5080/swagger`.

### Authentication
- `POST /api/auth/register` – validates data, checks the role is Organiser/Participant, rejects duplicate emails,
  hashes the password with PBKDF2 (random salt, 100 000 iterations) and saves the user. Only the hash is stored.
- `POST /api/auth/login` – verifies the password hash, then stores `UserId` and `Role` in a **server-side session**
  (cookie `.RaceDay.Session`, 30 min idle timeout).
- `POST /api/auth/logout` – clears the session.
- `[SessionAuthorize]` returns **401** when not logged in and **403** when the role is wrong. Ownership rules
  (an organiser may only change their own events/categories/enrolments/results) also return 403.
- Optional: set `Registration:OrganiserCode` in appsettings to require a code for Organiser sign-up.

### API Endpoints
| Area | Endpoint | Access |
|---|---|---|
| Auth | POST `/api/auth/register`, `/login`, `/logout` | Public |
| Profile | GET / PUT `/api/profile/me` | Any logged in |
| Events | GET `/api/events`, `/api/events/{id}` | Any logged in |
| Events | GET `/api/events/mine`, POST, PUT/DELETE `/api/events/{id}` | Organiser (owner) |
| Categories | GET `/api/events/{eventId}/categories` | Any logged in |
| Categories | POST `/api/events/{eventId}/categories`, PUT/DELETE `/api/categories/{id}` | Organiser (owner) |
| Enrolments | POST `/api/enrolments`, GET `/api/enrolments/mine` | Participant |
| Enrolments | GET `/api/enrolments/event/{eventId}`, PUT `/api/enrolments/{id}/status` | Organiser (owner) |
| Results | POST `/api/results`, GET `/api/results/event/{eventId}` | Organiser (owner) |
| Results | GET `/api/results/mine` | Participant |

Status codes: 200, 201, 204 (deletes), 400, 401, 403, 404, 409 (duplicates).


### Swagger
Open `/swagger`. Every endpoint has a summary, description, and documented responses. Suggested demo flow:
register Organiser -> login -> create event -> create categories -> logout -> register Participant -> login ->
enrol -> logout -> login as Organiser -> view enrolments -> capture result -> login as Participant -> view results.

### Unit Testing
```
dotnet test
```
Tests host the real API in memory with an InMemory database. They cover registration, login, logout, password hashing,
profile, event CRUD and ownership, categories, enrolments, results, and a role matrix proving Participants are
rejected from Organiser endpoints (403), Organisers from Participant endpoints (403) and anonymous users from every
protected endpoint (401).

### CI/CD
`.github/workflows/ci.yml` runs on every push: validates repo structure, restores, builds and runs all tests.

### GitHub Actions Screenshot


### Video Presentation

