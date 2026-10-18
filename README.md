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

The current health check confirms application availability
but does not verify database connectivity.

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

## DevOps Integration

The application is intended to integrate with:

- GitHub Actions for CI/CD
- Docker for application packaging
- Automated testing
- AWS for cloud deployment
- Amazon CloudWatch for monitoring

These infrastructure components are managed through
separate group workstreams.

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