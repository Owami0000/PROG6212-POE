# RaceDay

![CI](https://github.com/<your-username>/<your-repo>/actions/workflows/ci.yml/badge.svg)

## Project Overview

RaceDay is an event management system designed for running, walking and cycling events. It allows event organisers to create and manage events, register participants, manage event categories and publish race results.

Participants can browse upcoming events, enter events, choose their preferred categories and keep track of their enrolments and results. They can also manage their profiles and upload profile pictures.

The system uses an ASP.NET Core MVC application connected to a RESTful Web API. SQL Server stores the application data, while Azure Blob Storage is used to store images.

### How the System Works

- Participants and organisers access the system through a web browser.
- The MVC application handles the user interface.
- The API handles all requests and communication with the database.
- SQL Server stores user, event, enrolment and result information.
- Azure Blob Storage stores event banners and profile pictures.

The MVC application does not communicate directly with the database or Azure Storage. All these operations are handled through the API.

## User Roles

### Organiser

Organisers can:
- View a dashboard showing created events, enrolments and upcoming events.
- Create, edit and delete events.
- Upload event banner images.
- Create and manage event categories.
- View participant enrolments.
- Confirm or cancel enrolments.
- Add finishing times and positions.
- Publish event results.

### Participant

Participants can:
- Browse upcoming events.
- Search for events and filter by event type.
- View event details and available categories.
- Enrol in events by selecting a category.
- View their current and previous enrolments.
- Check their race results and finishing positions.
- Update their profile information.
- Upload a profile picture.

## Project Structure

The solution is organised into the following projects and folders:

- **docs:** Contains planning documents, including the ERD, API endpoint plan and SQL script.
- **RaceDay.Api:** Handles authentication, application data and communication with SQL Server and Azure Blob Storage.
- **RaceDay.Contracts:** Contains shared data transfer objects (DTOs).
- **RaceDay.Mvc:** Contains the website, controllers, views and user interface.
- **RaceDay.Api.Tests:** Contains automated tests for the API.
- **.github/workflows:** Contains the GitHub Actions CI/CD workflow.

## Part 1 – Planning and Database

The planning documents are available in the `docs` folder. These include:
- Entity Relationship Diagram (ERD).
- API endpoint plan.
- SQL database script.

## Part 2 – RESTful API

The API manages the main functionality of RaceDay, including:
- User registration and login.
- Password hashing and authentication.
- Role-based access for organisers and participants.
- Profile management.
- Event and category management.
- Participant enrolments.
- Race results.
- Image uploads.
- API documentation through Swagger.

The API also includes automated unit tests to check that its main features work correctly.

Swagger can be accessed at:

`http://localhost:5000/swagger`

## Part 3 – MVC Web Application

The MVC application provides the website that users interact with. It connects to the API to retrieve and update information.

### Requirements

Before running the project, make sure you have:

- .NET 8 SDK.
- SQL Server or SQL Server Express.
- Visual Studio 2022 or the .NET CLI.
- Docker Desktop with Linux containers and WSL 2.
- An Azure Storage account.

### Database Setup

1. Open `RaceDay.Api/appsettings.json`.
2. Update `ConnectionStrings:DefaultConnection` with your SQL Server connection string.
3. Open the Package Manager Console and select `RaceDay.Api` as the default project.
4. Run the following commands:

```powershell
Add-Migration InitialRaceDayDatabase
Add-Migration AddImageUrls
Update-Database
```

If the initial migration has already been applied, only run:

```powershell
Add-Migration AddImageUrls
Update-Database
```

Afterwards, check in SQL Server Management Studio (SSMS) that the `Events` table contains `BannerImageUrl` and the `Users` table contains `ProfileImageUrl`.

### Azure Blob Storage Setup

Azure Blob Storage is used to store event banners and participant profile pictures.

1. Create a Storage Account in the Azure portal.
2. Enable anonymous blob access in the storage account configuration.
3. Copy the storage account connection string from Access Keys.
4. Store the connection string using User Secrets:

```powershell
dotnet user-secrets set "AzureBlobStorage:ConnectionString" "<your connection string>" --project RaceDay.Api
```

Do not upload your connection string or other sensitive credentials to GitHub.

The API automatically creates the `raceday-images` container.

### Running the API

Start the API using:

```bash
dotnet run --project RaceDay.Api --urls http://0.0.0.0:5000
```

Check that the API is running by opening:

`http://localhost:5000/swagger`

Keep the API running while using the MVC application.

### Running the MVC Application

Open another terminal and run:

```bash
dotnet run --project RaceDay.Mvc
```

Open the website at:

`http://localhost:5100`

The MVC application uses the API address configured in `RaceDay.Mvc/appsettings.json`.

### Running with Docker

Make sure Docker Desktop is running.

Build the Docker image from the folder containing `RaceDay.sln`:

```bash
docker build -f RaceDay.Mvc/Dockerfile -t raceday-mvc:part3 .
```

Run the container:

```bash
docker run -d --name raceday-mvc -p 8080:8080 -e ApiSettings__BaseUrl=http://host.docker.internal:5000/ raceday-mvc:part3
```

Open the website at:

`http://localhost:8080`

The API must still be running on the host computer while the MVC application runs inside Docker.

Useful Docker commands:

```bash
docker ps
docker logs raceday-mvc
docker rm -f raceday-mvc
```

### Authentication and Sessions

RaceDay uses session-based authentication.

1. Users log in through the MVC application.
2. The MVC application sends their login details to the API.
3. The API verifies the password and creates a session.
4. The MVC application stores the session information and uses it for future API requests.
5. Access is restricted based on the user's role.
6. When a user logs out, their session is cleared.

If a session expires, the user is redirected to the login page.

## Main Application Features

**Organiser:**
- Dashboard.
- Event management.
- Category management.
- Enrolment management.
- Results management.

**Participant:**
- Browse and search events.
- View event information.
- Enrol in events.
- View enrolments.
- View race results.
- Manage profile information and profile pictures.

## API Endpoints

| Area | Endpoint | Access |
|---|---|---|
| Authentication | `POST /api/auth/register` | Public |
| Authentication | `POST /api/auth/login` | Public |
| Authentication | `POST /api/auth/logout` | Logged-in user |
| Profile | `GET/PUT /api/profile/me` | Logged-in user |
| Profile | `POST /api/profile/me/image` | Logged-in user |
| Events | `GET /api/events` | Logged-in user |
| Events | `GET /api/events/{id}` | Logged-in user |
| Events | `POST /api/events` | Organiser |
| Events | `PUT/DELETE /api/events/{id}` | Organiser |
| Events | `POST /api/events/{id}/banner` | Organiser |
| Categories | `GET /api/events/{eventId}/categories` | Logged-in user |
| Categories | `POST /api/events/{eventId}/categories` | Organiser |
| Categories | `PUT/DELETE /api/categories/{id}` | Organiser |
| Enrolments | `POST /api/enrolments` | Participant |
| Enrolments | `GET /api/enrolments/mine` | Participant |
| Enrolments | `GET /api/enrolments/event/{eventId}` | Organiser |
| Enrolments | `PUT /api/enrolments/{id}/status` | Organiser |
| Results | `POST /api/results` | Organiser |
| Results | `GET /api/results/event/{eventId}` | Organiser |
| Results | `GET /api/results/mine` | Participant |

## Changes from Previous Parts

The following additions were made when developing Part 3:

- Added `BannerImageUrl` to the Events table.
- Added `ProfileImageUrl` to the Users table.
- Added image upload functionality using Azure Blob Storage.
- Added enrolment counts to event information.
- Added event dates and total finisher information to result data.
- Added the MVC application to provide a user-friendly interface.
- Added Docker support for running the MVC application.

## Unit Testing

Run the automated tests using:

```bash
dotnet test RaceDay.Api.Tests/RaceDay.Api.Tests.csproj
```

The tests cover authentication, profiles, events, categories, enrolments, results and image uploads.

They also check user permissions, invalid requests and different access levels. The tests use an InMemory database and a fake blob storage service, so an actual Azure connection is not required.

## CI/CD – GitHub Actions

RaceDay uses GitHub Actions to automatically check the project whenever changes are pushed to the repository or a pull request is created.

The workflow:
- Checks the repository structure.
- Restores the required packages.
- Builds the entire solution.
- Runs the automated unit tests.
- Builds the MVC Docker image.

### GitHub Actions Screenshot

![Successful green build](docs/ci-green-build.png)

## Video Presentation

YouTube video presentation:

**[https://youtu.be/XUMtbU-lIrg]**
