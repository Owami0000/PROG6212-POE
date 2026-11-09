# RaceDay

![CI](https://github.com/<your-username>/<your-repo>/actions/workflows/ci.yml/badge.svg)

## Project Overview
RaceDay is an event management system for running, walking and cycling events.
Organisers create events (with banner images) and categories, manage enrolments and publish results.
Participants browse upcoming events, enter with a category, and view their enrolments, results and profile.

```
Browser ──► RaceDay.Mvc (ASP.NET Core MVC, Docker) ──► RaceDay.Api (Web API) ──► SQL Server
                                                              └──────────────► Azure Blob Storage (images)
```
The MVC project contains **no database code and no Azure code**: every data operation and every file upload goes through the API.

## User Roles
### Organiser
- Dashboard: events created, enrolments per event, upcoming dates
- Create / edit / delete events (name, description, date, location, distance, type Run/Walk/Cycle) and upload a banner image
- Add / edit / delete categories (e.g. Under 20, Senior, 10km)
- View enrolments per event (participant, category, status) and confirm / cancel them
- Capture and publish finish times and finishing positions

### Participant
- Upcoming events with search and type filter
- Event details (banner, full information, categories) and enrolment with a category
- My Events (enrolments with colour-coded status), My Results (event, date, category, finish time, e.g. *3rd out of 24 finishers*)
- Profile: view/update details and upload a profile picture

## Solution Structure
```
RaceDay
├── docs/                    Part 1 ERD, endpoint plan, SQL script
├── RaceDay.Api/             Part 2 + Part 3 additions (Azure Blob service, image endpoints)
├── RaceDay.Contracts/       Shared DTOs used by the API and the MVC app
├── RaceDay.Mvc/             Part 3 MVC front end
│   ├── Controllers/         Auth, Home, Events, Organiser, Participant
│   ├── Services/            RaceDayApiClient (all API calls, API session cookie handling)
│   ├── ViewModels/  Helpers/  Views/  wwwroot/
│   └── Dockerfile
├── RaceDay.Api.Tests/       xUnit tests for the API
├── .github/workflows/ci.yml
├── .dockerignore
└── README.md
```

## Part 1 - Planning and Database
Planning documents are in [`docs`](docs): ERD, API endpoint plan and SQL script.

## Part 2 - RESTful API
Authentication (PBKDF2-hashed passwords, server-side session storing UserId and Role), role-based access (401 / 403),
profiles, events, categories, enrolments, results, Swagger documentation and unit tests.
See the endpoint table below. Swagger: `http://localhost:5000/swagger`.

## Part 3 - MVC Web Application
### Prerequisites
.NET 8 SDK, SQL Server / SQL Express, Visual Studio 2022 (or the `dotnet` CLI), Docker Desktop (Linux containers, WSL 2),
and an Azure Storage account (for image uploads).

### Database Setup
1. Set `ConnectionStrings:DefaultConnection` in `RaceDay.Api/appsettings.json` for your SQL Server instance.
2. In Package Manager Console (default project `RaceDay.Api`):
   ```
   Add-Migration InitialRaceDayDatabase
   Add-Migration AddImageUrls
   Update-Database
   ```
   (If `InitialRaceDayDatabase` was already applied in Part 2, only run `Add-Migration AddImageUrls` and `Update-Database`.)
   CLI: `dotnet ef migrations add AddImageUrls --project RaceDay.Api` then `dotnet ef database update --project RaceDay.Api`.
3. Confirm in SSMS that `Events.BannerImageUrl` and `Users.ProfileImageUrl` exist.

### Azure Blob Storage Setup
1. In the Azure portal create a **Storage account** (Standard, LRS).
2. *Settings > Configuration*: enable **Allow Blob anonymous access** (needed so the browser can display blob URLs).
   The API creates the container `raceday-images` with blob-level public read automatically.
3. *Security + networking > Access keys*: copy a **connection string**.
4. Store it as a secret - **never commit it**:
   ```
   dotnet user-secrets set "AzureBlobStorage:ConnectionString" "<your connection string>" --project RaceDay.Api
   ```
   (or set the environment variable `AzureBlobStorage__ConnectionString`).

Upload flow: browser → MVC → `POST /api/events/{id}/banner` or `POST /api/profile/me/image` → API → Azure Blob Storage →
blob URL saved in SQL Server → URL returned and displayed in MVC.

### How to Run RaceDay.Api
Docker needs the API reachable from the host network, so start it on port 5000 over HTTP:
```
dotnet run --project RaceDay.Api --urls http://0.0.0.0:5000
```
Check `http://localhost:5000/swagger`. Leave this terminal running.

### How to Run RaceDay.Mvc Locally
```
dotnet run --project RaceDay.Mvc
```
Open `http://localhost:5100`. The API address comes from `RaceDay.Mvc/appsettings.json` → `ApiSettings:BaseUrl` (`http://localhost:5000/`).

### Docker Setup
Verify Docker first: `docker --version` and `docker info`.

#### Build Image
Run from the folder containing `RaceDay.sln`:
```
docker build -f RaceDay.Mvc/Dockerfile -t raceday-mvc:part3 .
```

#### Run Container
With the API running on the host (see above):
```
docker run -d --name raceday-mvc -p 8080:8080 -e ApiSettings__BaseUrl=http://host.docker.internal:5000/ raceday-mvc:part3
```
Open `http://localhost:8080`. Inside a container `localhost` means the container itself, so the API on the Windows host is
reached through `host.docker.internal`. Useful commands: `docker ps`, `docker logs raceday-mvc`, `docker rm -f raceday-mvc`.

> **Tested command:** _confirm that you ran exactly this command successfully on your machine before submitting._

### Authentication and Session Flow
1. MVC posts the login to `POST /api/auth/login`.
2. The API verifies the password hash, creates its server-side session and returns `Set-Cookie: .RaceDay.Session=...`.
3. `RaceDayApiClient` stores that API cookie in the **MVC session** together with `UserId`, `Role` and `DisplayName`,
   and sends it as the `Cookie` header on every later API call (cookie handling is per user; the shared `HttpClient`
   handler has `UseCookies = false`).
4. MVC controllers check the MVC session: not logged in → redirect to Login; wrong role → Access Denied page.
   If the API answers 401 (its session timed out) the user is sent back to Login with a friendly message.
5. Logout calls `POST /api/auth/logout` and clears the MVC session.

### Organiser Features
Dashboard · Events (create / edit / delete / banner) · Categories · Enrolments (confirm / cancel) · Results (capture & publish).

### Participant Features
Upcoming events + filters · Event details & enrolment · My Events · My Results · Profile + profile picture.

### API Endpoints
| Area | Endpoint | Access |
|---|---|---|
| Auth | `POST /api/auth/register`, `/login`, `/logout` | Public |
| Profile | `GET/PUT /api/profile/me`, `POST /api/profile/me/image` | Any logged-in user |
| Events | `GET /api/events`, `GET /api/events/{id}` | Any logged-in user |
| Events | `GET /api/events/mine`, `POST`, `PUT/DELETE /api/events/{id}`, `POST /api/events/{id}/banner` | Organiser (owner) |
| Categories | `GET /api/events/{eventId}/categories` | Any logged-in user |
| Categories | `POST /api/events/{eventId}/categories`, `PUT/DELETE /api/categories/{id}` | Organiser (owner) |
| Enrolments | `POST /api/enrolments`, `GET /api/enrolments/mine` | Participant |
| Enrolments | `GET /api/enrolments/event/{eventId}`, `PUT /api/enrolments/{id}/status` | Organiser (owner) |
| Results | `POST /api/results`, `GET /api/results/event/{eventId}` | Organiser (owner) |
| Results | `GET /api/results/mine` | Participant |

**Changes from Part 1/Part 2:** `BannerImageUrl` (Events) and `ProfileImageUrl` (Users) columns added for Azure images;
`EventDto.EnrolmentCount`, `ResultDto.EventDate` and `ResultDto.TotalFinishers` added so the dashboard and
"3rd out of 24 finishers" can be shown without MVC touching the database. _(Add anything else you changed.)_

### Unit Testing
```
dotnet test RaceDay.Api.Tests/RaceDay.Api.Tests.csproj
```
Tests run the real API in memory with an InMemory database and a fake blob service (no Azure needed): authentication,
profile, events, categories, enrolments, results, image uploads (success, wrong role, non-image, too large), a role matrix
(403 / 401 on every protected endpoint), enrolment counts and finisher totals.

## CI/CD
`.github/workflows/ci.yml` runs on every push and pull request: validates the repository structure, restores, builds the
**whole solution including RaceDay.Mvc**, runs the unit tests, and then builds the MVC Docker image.

### GitHub Actions Screenshot
![Successful green build](docs/ci-green-build.png)

## Video Presentation
Unlisted YouTube video: **<paste link here>**
