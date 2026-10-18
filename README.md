# SWE40006 TaskTracker

TaskTracker is a web-based task management application developed for
SWE40006 Software Deployment and Evolution at Swinburne University
of Technology.

The project demonstrates application development, database
persistence and integration with a DevOps deployment pipeline.

## Application Overview

TaskTracker allows users to create, manage and track tasks
through a web interface.

The application includes a dashboard displaying task progress,
deadlines and priorities.

## Features

- Create tasks
- View existing tasks
- Edit task details
- Mark tasks as completed
- Delete tasks
- Set task priorities
- Set task deadlines
- Dashboard statistics
- Task status board
- SQLite database persistence
- Input validation
- Application health-check endpoint

## Dashboard Views

The interface includes three demonstration perspectives:

- Team Member
- Department Manager
- Workspace Owner

The dropdown changes the dashboard information displayed.

These are demonstration views only. Authentication,
employee-specific permissions and task assignment are
not implemented in the current version.

## Technology Stack

| Component | Technology |
|-----------|------------|
| Language | C# |
| Framework | ASP.NET Core Razor Pages |
| Runtime | .NET 10 |
| Database | SQLite |
| ORM | Entity Framework Core 10 |
| Frontend | HTML, CSS, Bootstrap |
| Version Control | Git / GitHub |

## Requirements

Before running the application, install:

- .NET 10 SDK
- Git
- Visual Studio Code or another compatible IDE

## Running the Application

Clone the repository:

```bash
git clone YOUR_REPOSITORY_URL
```

Navigate to the application directory:

```bash
cd SWE40006-TaskTracker/TaskTracker
```

Restore dependencies:

```bash
dotnet restore
```

Build the application:

```bash
dotnet build
```

Create or update the SQLite database:

```bash
dotnet ef database update
```

Run the application:

```bash
dotnet run
```

Open the localhost address displayed in the terminal.

The port may vary depending on the environment.

## Database

TaskTracker uses SQLite through Entity Framework Core.

The default development connection string is:

```text
Data Source=tasktracker.db
```

Database migrations are included in the source repository.

The application also applies pending migrations at startup.

Local SQLite database files are excluded from Git.

## Deployment Configuration

The database connection string can be overridden using
an environment variable:

```text
ConnectionStrings__DefaultConnection
```

Example Docker configuration:

```text
ConnectionStrings__DefaultConnection=Data Source=/app/data/tasktracker.db
```

The deployment environment must provide persistent storage
for the SQLite database.

## Health Check

The application provides a health-check endpoint:

```text
/health
```

A healthy application returns an HTTP 200 response.

The health check includes a database check that queries the
Tasks table, so it reports `Unhealthy` when the SQLite
database is unavailable.

The `/version` endpoint returns the Git commit the running
build was made from:

```json
{"commit": "<sha>", "environment": "Production"}
```

## Project Structure

```text
TaskTracker/
├── Data/
├── Models/
├── Pages/
│   ├── Tasks/
│   ├── Index.cshtml
│   └── Shared/
├── Migrations/
├── wwwroot/
├── Program.cs
├── appsettings.json
└── TaskTracker.csproj
```

## Running in Docker

```bash
GIT_SHA=$(git rev-parse HEAD) docker compose up --build -d
```

The application is then available at http://localhost:8080.
Task data is kept in the `tasktracker-data` volume and survives
container replacement.

## CI/CD Pipeline

Every push to `main` runs `.github/workflows/ci-cd.yml`:

1. Build and run the unit and integration tests with a coverage gate.
2. Build the container image, test it, and publish it to GitHub
   Container Registry tagged with the commit SHA.
3. Deploy the image to the staging EC2 server and run the smoke
   and end-to-end tests against it.
4. Promote the same image to the production EC2 server, run the
   production smoke test, and roll back automatically if it fails.

Pull requests run steps 1 and 2 without deploying.

See [docs/PIPELINE.md](docs/PIPELINE.md) for configuration,
recovery steps and the evidence checklist.

## Current Limitations

- No user authentication
- No enforced role-based permissions
- No employee task assignment
- No department management
- No multi-user isolation
- SQLite is configured for a single application instance

The application currently uses a development-only role
preview feature.

It should not be exposed as an unrestricted public
business application without authentication and
authorisation controls.

## Group Responsibilities

| Workstream | Responsibility |
|------------|----------------|
| Application | ASP.NET Core, SQLite and frontend |
| CI/CD | GitHub Actions and Docker |
| Testing | Automated testing and verification |
| Infrastructure | AWS deployment and monitoring |

## Course

SWE40006 - Software Deployment and Evolution

Swinburne University of Technology