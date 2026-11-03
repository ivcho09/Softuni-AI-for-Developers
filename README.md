# TaskFlow

A personal productivity REST API for managing tasks and projects, built with ASP.NET Core and
Entity Framework Core on top of SQLite.

TaskFlow lets you create projects, organise tasks inside them, assign priorities and deadlines,
track completion, filter and search your task list, and read productivity statistics calculated
from your actual data. Everything is persisted to a SQLite database file, so your data survives
restarts. Every endpoint is documented and directly testable through Swagger UI.

---

## Table of contents

- [Features](#features)
- [Technology stack](#technology-stack)
- [Architecture](#architecture)
- [Getting started](#getting-started)
- [Database and migrations](#database-and-migrations)
- [API reference](#api-reference)
- [Example requests and responses](#example-requests-and-responses)
- [Filtering, searching, sorting and pagination](#filtering-searching-sorting-and-pagination)
- [Productivity statistics](#productivity-statistics)
- [Error handling](#error-handling)
- [Testing](#testing)
- [Design decisions](#design-decisions)
- [Limitations](#limitations)

---

## Features

**Task management**
- Full CRUD for tasks
- Mark a task completed, and reopen it again
- `CreatedAt` is assigned by the server; `CompletedAt` is stamped on completion and cleared on reopen
- Three statuses (`Pending`, `InProgress`, `Completed`) and four priorities (`Low`, `Medium`, `High`, `Urgent`)
- Optional deadline and optional owning project

**Project management**
- Full CRUD for projects
- List every task belonging to a project
- Deleting a project **never deletes its tasks** — they are kept and detached (`projectId` becomes `null`)

**Querying**
- Filter by status, priority, project and overdue state
- Case-insensitive title search
- Sort by creation date, deadline, priority or title, ascending or descending
- Pagination with full metadata (`totalCount`, `totalPages`, `hasNextPage`, …)

**Statistics**
- Overall and per-project productivity figures, calculated in the database
- Totals per status and per priority, overdue counts and a completion percentage

**Cross-cutting**
- Input validation with FluentValidation and one consistent error shape for every failure
- Centralised exception handling — no `try`/`catch` anywhere in the controllers
- Swagger UI with descriptions, request/response schemas and examples on every endpoint
- 86 automated unit and integration tests

---

## Technology stack

| Concern | Choice |
|---|---|
| Language / framework | C# 14, ASP.NET Core Web API, **.NET 10** |
| Data access | Entity Framework Core 10 |
| Database | SQLite (file-backed) |
| Validation | FluentValidation 12 |
| API documentation | `Microsoft.AspNetCore.OpenApi` (OpenAPI 3.1) + Swashbuckle Swagger UI |
| Testing | xUnit, `Microsoft.AspNetCore.Mvc.Testing` |

---

## Architecture

The application is split into layers with one responsibility each. Controllers never contain
business logic, and database entities are never exposed over HTTP.

```
HTTP request
     │
     ▼
┌─────────────────────────────────────────────────────────┐
│ ExceptionHandlingMiddleware                             │  maps exceptions → status codes
│  ┌───────────────────────────────────────────────────┐  │
│  │ ValidationFilter                                  │  │  runs FluentValidation on every argument
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │ Controllers  (thin: bind → call → status)   │  │  │
│  │  │        │                                    │  │  │
│  │  │        ▼                                    │  │  │
│  │  │ Services via Interfaces                     │  │  │  all business rules live here
│  │  │   ITaskService / IProjectService /          │  │  │
│  │  │   IStatisticsService                        │  │  │
│  │  │        │                                    │  │  │
│  │  │        ▼                                    │  │  │
│  │  │ TaskFlowDbContext  (EF Core)                │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
     │
     ▼
  taskflow.db  (SQLite)
```

**Request flow for `POST /api/tasks`**

1. `ValidationFilter` resolves `IValidator<CreateTaskRequest>` and checks the payload.
   On failure it short-circuits with `400` and never reaches the controller.
2. `TasksController.CreateTask` calls `ITaskService.CreateTaskAsync` and returns `201 Created`.
3. `TaskService` verifies the referenced project exists, assigns `CreatedAt`, applies the
   `CompletedAt` rule, saves, and maps the entity to a `TaskResponse`.
4. Any `NotFoundException` or `ValidationException` thrown on the way is translated by
   `ExceptionHandlingMiddleware` into a `404` or `400` with the standard error body.

### Project structure

```
TaskFlow/
├── TaskFlow.sln
├── README.md
├── .gitignore
│
├── TaskFlow/                          # the Web API
│   ├── Controllers/                   # TasksController, ProjectsController, StatisticsController
│   ├── Models/                        # TaskItem, Project, enums, shared validation limits
│   ├── DTOs/
│   │   ├── Common/                    # ErrorResponse, PagedResponse<T>
│   │   ├── Tasks/                     # create/update requests, TaskResponse, query parameters
│   │   ├── Projects/                  # create/update requests, ProjectResponse
│   │   └── Statistics/                # StatisticsResponse, ProjectStatisticsResponse
│   ├── Data/                          # TaskFlowDbContext
│   ├── Services/                      # TaskService, ProjectService, StatisticsService
│   ├── Interfaces/                    # ITaskService, IProjectService, IStatisticsService
│   ├── Validators/                    # FluentValidation validators, filter, error formatter
│   ├── Middleware/                    # ExceptionHandlingMiddleware
│   ├── Exceptions/                    # NotFoundException
│   ├── Migrations/                    # EF Core migrations
│   ├── Program.cs                     # composition root and HTTP pipeline
│   ├── TaskFlow.http                  # ready-to-run example requests
│   └── appsettings.json
│
└── TaskFlow.Tests/
    ├── Unit/                          # service and validator tests (in-memory SQLite)
    └── Integration/                   # endpoint tests over HTTP (isolated SQLite file)
```

---

## Getting started

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer
  (check with `dotnet --version`)

That is all. SQLite needs no installation or server — the database is a single file created
automatically.

### Run the application

```bash
git clone <your-repository-url>
cd TaskFlow

dotnet restore
dotnet run --project TaskFlow
```

The API starts on **<http://localhost:5171>** and the database file `TaskFlow/taskflow.db` is
created and migrated automatically on first start.

Then open:

| | |
|---|---|
| **Swagger UI** | <http://localhost:5171/swagger> |
| OpenAPI document | <http://localhost:5171/openapi/v1.json> |

Browsing <http://localhost:5171> redirects to Swagger UI.

> To use the HTTPS profile instead, run `dotnet run --project TaskFlow --launch-profile https`
> (<https://localhost:7277>). You may need `dotnet dev-certs https --trust` once.

### Try it from the editor

`TaskFlow/TaskFlow.http` contains a runnable request for every endpoint, including the
filtering combinations and the error cases. Open it in Visual Studio, VS Code (REST Client
extension) or Rider and click *Send Request*.

---

## Database and migrations

The schema is defined by EF Core migrations under `TaskFlow/Migrations/`.

`Program.cs` calls `Database.MigrateAsync()` at startup, so **a fresh clone needs no manual
step** — the database is created and brought up to date automatically. The equivalent
commands, if you prefer to run them yourself:

```bash
# one-off: install the EF Core CLI tool
dotnet tool install --global dotnet-ef

# apply all migrations (creates taskflow.db if missing)
dotnet ef database update --project TaskFlow --startup-project TaskFlow

# add a migration after changing an entity
dotnet ef migrations add <MigrationName> --project TaskFlow --startup-project TaskFlow --output-dir Migrations

# inspect the SQL a migration would run
dotnet ef migrations script --project TaskFlow --startup-project TaskFlow
```

The connection string lives in `appsettings.json` and can be overridden per environment:

```json
{
  "ConnectionStrings": {
    "TaskFlowDatabase": "Data Source=taskflow.db"
  }
}
```

```bash
# example: point at a different file without editing the config
ConnectionStrings__TaskFlowDatabase="Data Source=/tmp/demo.db" dotnet run --project TaskFlow
```

### Schema

**Tasks**

| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER | primary key, autoincrement |
| `Title` | TEXT | required, max 100 characters |
| `Description` | TEXT | optional, max 1000 characters |
| `Status` | INTEGER | `0` Pending, `1` InProgress, `2` Completed |
| `Priority` | INTEGER | `0` Low, `1` Medium, `2` High, `3` Urgent |
| `CreatedAt` | TEXT | UTC, assigned by the server |
| `DueDate` | TEXT | UTC, nullable |
| `CompletedAt` | TEXT | UTC, nullable |
| `ProjectId` | INTEGER | nullable FK → `Projects.Id`, **`ON DELETE SET NULL`** |

Indexed on `Status`, `Priority`, `DueDate` and `ProjectId` to support the filtering endpoints.

**Projects**

| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER | primary key, autoincrement |
| `Name` | TEXT | required, max 100 characters, indexed |
| `Description` | TEXT | optional, max 1000 characters |
| `CreatedAt` | TEXT | UTC, assigned by the server |

---

## API reference

Base URL: `http://localhost:5171/api`

### Tasks

| Method | Route | Description | Success | Errors |
|---|---|---|---|---|
| `GET` | `/tasks` | List tasks with filtering, search, sorting and pagination | `200` | `400` |
| `GET` | `/tasks/{id}` | Get a single task | `200` | `404` |
| `POST` | `/tasks` | Create a task | `201` | `400` |
| `PUT` | `/tasks/{id}` | Replace a task | `200` | `400`, `404` |
| `DELETE` | `/tasks/{id}` | Delete a task | `204` | `404` |
| `POST` | `/tasks/{id}/complete` | Mark completed, stamp `completedAt` | `200` | `404` |
| `POST` | `/tasks/{id}/reopen` | Return to `Pending`, clear `completedAt` | `200` | `404` |

### Projects

| Method | Route | Description | Success | Errors |
|---|---|---|---|---|
| `GET` | `/projects` | List all projects with task counts | `200` | — |
| `GET` | `/projects/{id}` | Get a single project | `200` | `404` |
| `GET` | `/projects/{id}/tasks` | List the project's tasks | `200` | `404` |
| `POST` | `/projects` | Create a project | `201` | `400` |
| `PUT` | `/projects/{id}` | Replace a project | `200` | `400`, `404` |
| `DELETE` | `/projects/{id}` | Delete a project, detaching its tasks | `204` | `404` |

### Statistics

| Method | Route | Description | Success | Errors |
|---|---|---|---|---|
| `GET` | `/statistics` | Productivity statistics across all tasks | `200` | — |
| `GET` | `/statistics/projects/{projectId}` | Statistics for one project | `200` | `404` |

---

## Example requests and responses

### Create a project

```http
POST /api/projects
Content-Type: application/json

{
  "name": "University",
  "description": "Coursework and exams"
}
```

```http
201 Created
Location: http://localhost:5171/api/projects/1
```
```json
{
  "id": 1,
  "name": "University",
  "description": "Coursework and exams",
  "createdAt": "2026-11-03T12:06:12.8735632Z",
  "taskCount": 0
}
```

### Create a task

```http
POST /api/tasks
Content-Type: application/json

{
  "title": "Finish database homework",
  "description": "Normalise the schema",
  "priority": "High",
  "dueDate": "2026-12-01T09:00:00Z",
  "projectId": 1
}
```

```json
{
  "id": 1,
  "title": "Finish database homework",
  "description": "Normalise the schema",
  "status": "Pending",
  "priority": "High",
  "createdAt": "2026-11-03T12:06:13.3398999Z",
  "dueDate": "2026-12-01T09:00:00Z",
  "completedAt": null,
  "projectId": 1,
  "projectName": "University",
  "isOverdue": false
}
```

`status` and `priority` default to `Pending` and `Medium` when omitted. `dueDate` and
`projectId` are optional.

### Complete and reopen a task

```http
POST /api/tasks/1/complete
```
```json
{ "id": 1, "status": "Completed", "completedAt": "2026-11-03T12:06:23.2021022Z", "...": "..." }
```

```http
POST /api/tasks/1/reopen
```
```json
{ "id": 1, "status": "Pending", "completedAt": null, "...": "..." }
```

### List tasks (paginated)

```http
GET /api/tasks?status=Pending&sortBy=dueDate&page=1&pageSize=2
```
```json
{
  "items": [
    { "id": 6, "title": "Fix the sink", "status": "Pending", "priority": "Medium", "isOverdue": true, "...": "..." },
    { "id": 1, "title": "Maths homework", "status": "Pending", "priority": "High", "isOverdue": true, "...": "..." }
  ],
  "page": 1,
  "pageSize": 2,
  "totalCount": 5,
  "totalPages": 3,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

### Delete a project without losing its tasks

```http
DELETE /api/projects/2
```
```http
204 No Content
```
```http
GET /api/tasks/6
```
```json
{
  "id": 6,
  "title": "Fix the sink",
  "projectId": null,
  "projectName": null,
  "...": "..."
}
```

---

## Filtering, searching, sorting and pagination

All parameters on `GET /api/tasks` are optional and can be combined freely.

| Parameter | Type | Description |
|---|---|---|
| `status` | `Pending` \| `InProgress` \| `Completed` | Only tasks in this status |
| `priority` | `Low` \| `Medium` \| `High` \| `Urgent` | Only tasks with this priority |
| `projectId` | integer | Only tasks in this project |
| `overdue` | boolean | `true` → only overdue tasks; `false` → only tasks that are not overdue |
| `search` | string | Case-insensitive substring match on the title |
| `sortBy` | `createdAt` \| `dueDate` \| `priority` \| `title` | Sort field (default `createdAt`) |
| `descending` | boolean | Sort direction. Defaults to ascending, except `priority`, which defaults to most-urgent-first |
| `page` | integer ≥ 1 | Page number (default `1`) |
| `pageSize` | 1–100 | Records per page (default `20`) |

```http
GET /api/tasks?status=Pending&priority=High
GET /api/tasks?overdue=true
GET /api/tasks?search=homework
GET /api/tasks?sortBy=dueDate
GET /api/tasks?sortBy=priority
GET /api/tasks?page=2&pageSize=10
```

Notes:

- **Overdue** means `dueDate` is in the past **and** the task is not completed. A task without a
  deadline is never overdue.
- Sorting by `dueDate` places tasks **without** a deadline after those that have one.
- `%` and `_` in `search` are matched literally, not as SQL wildcards.

---

## Productivity statistics

```http
GET /api/statistics
```
```json
{
  "totalTasks": 7,
  "completedTasks": 1,
  "pendingTasks": 5,
  "inProgressTasks": 1,
  "overdueTasks": 3,
  "completionPercentage": 14.29,
  "tasksByPriority": { "Low": 2, "Medium": 2, "High": 2, "Urgent": 1 },
  "tasksByStatus": { "Pending": 5, "InProgress": 1, "Completed": 1 }
}
```

```http
GET /api/statistics/projects/1
```
```json
{
  "projectId": 1,
  "projectName": "University",
  "totalTasks": 4,
  "completedTasks": 1,
  "pendingTasks": 2,
  "inProgressTasks": 1,
  "overdueTasks": 2,
  "completionPercentage": 25,
  "tasksByPriority": { "Low": 1, "Medium": 0, "High": 2, "Urgent": 1 },
  "tasksByStatus": { "Pending": 2, "InProgress": 1, "Completed": 1 }
}
```

Calculation rules:

- `completedTasks` counts only tasks with status `Completed`.
- `pendingTasks` counts only status `Pending`, so completed tasks are never included.
- `overdueTasks` counts tasks whose `dueDate` is earlier than the current time **and** that are
  not completed.
- `completionPercentage` = `completedTasks / totalTasks * 100`, rounded to two decimals, and
  **`0` when there are no tasks**.
- `tasksByPriority` and `tasksByStatus` always list every value, including zeroes, so the
  response shape is stable.

All figures are aggregated by the database with `GROUP BY`/`COUNT` rather than by loading
tasks into memory.

---

## Error handling

Every failure returns the same JSON shape, produced centrally by
`ExceptionHandlingMiddleware` and the validation filter.

| Status | When |
|---|---|
| `200 OK` | Successful read or update |
| `201 Created` | Resource created (includes a `Location` header) |
| `204 No Content` | Successful delete |
| `400 Bad Request` | Validation failure, unparseable body, unsupported enum value, or a reference to a nonexistent project |
| `404 Not Found` | The requested task or project does not exist |
| `500 Internal Server Error` | Unexpected server fault (message is generic; details are logged) |

**Validation failure**

```json
{
  "status": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "title": ["Title is required."],
    "description": ["Description cannot exceed 1000 characters."]
  },
  "traceId": "0HNPPKBA9DBQJ:00000001"
}
```

**Unsupported enum value** — the message lists what is accepted:

```json
{
  "status": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "status": ["'Bogus' is not a supported value. Allowed values: Pending, InProgress, Completed."]
  },
  "traceId": "0HNPPKCFBE85C:00000001"
}
```

**Not found**

```json
{
  "status": 404,
  "message": "Task with id 987654 was not found.",
  "traceId": "0HNPPKCFBE864:00000001"
}
```

Validation rules enforced:

- Task title is required and limited to 100 characters
- Descriptions are limited to 1000 characters
- Project name is required and limited to 100 characters
- `dueDate`, when supplied, must fall between 2000-01-01 and 2100-01-01
- `status` and `priority` must be supported values
- `projectId` must reference an existing project
- `page` ≥ 1 and `pageSize` between 1 and 100

---

## Testing

```bash
# run everything
dotnet test

# with per-test output
dotnet test --logger "console;verbosity=detailed"

# only the unit tests / only the integration tests
dotnet test --filter "FullyQualifiedName~Unit"
dotnet test --filter "FullyQualifiedName~Integration"
```

Current state: **86 tests, all passing.**

```
Passed!  - Failed: 0, Passed: 86, Skipped: 0, Total: 86
```

**Unit tests** (`TaskFlow.Tests/Unit/`) exercise the services and validators directly against a
private in-memory SQLite database created from the real migrations — so the SQL translation,
constraints and delete behaviour are genuine, not a simplified in-memory fake.

**Integration tests** (`TaskFlow.Tests/Integration/`) boot the real application with
`WebApplicationFactory` and drive it over HTTP. Only the connection string is overridden: each
test class gets its own SQLite file in the temp directory, which is deleted afterwards, so a
test run **never touches `taskflow.db`**.

Both suites inject a frozen clock (`FixedTimeProvider`) instead of using the real time, which is
what makes deadline, overdue and completion-timestamp assertions deterministic. Every test
creates its own data, so the suite is repeatable and order-independent.

Coverage includes:

| Area | Covered |
|---|---|
| Creating, retrieving, updating and deleting tasks | ✅ |
| Completing and reopening tasks, including idempotency | ✅ |
| `CreatedAt` assignment and the `CompletedAt` set/clear rule | ✅ |
| Creating and retrieving projects, task counts | ✅ |
| Deleting a project detaches rather than deletes its tasks | ✅ |
| Rejecting invalid input (empty/over-long fields, bad enums, bad paging) | ✅ |
| Rejecting tasks that reference a nonexistent project | ✅ |
| Filtering by status, priority, project and overdue state | ✅ |
| Search, including literal handling of `%` | ✅ |
| Sorting by each field, nulls-last on deadlines | ✅ |
| Pagination and its metadata | ✅ |
| Statistics figures, percentage rounding, and the zero-tasks case | ✅ |
| Overdue logic following the clock | ✅ |
| `404` for every nonexistent-resource route, with the error contract | ✅ |

---

## Design decisions

**`TaskItem` instead of `Task`.** With implicit usings, `System.Threading.Tasks.Task` is in
scope everywhere, so an entity named `Task` would force awkward qualification in every async
service signature. The HTTP surface still speaks of "tasks" — only the C# type differs.

**Enums stored as integers.** The ordinals are deliberate (`Low = 0` … `Urgent = 3`) so that
`ORDER BY Priority` ranks correctly in SQL. Stored as text, sorting would be alphabetical —
*High, Low, Medium, Urgent* — which is meaningless. Over the wire they are still readable names.

**UTC value converters on all dates.** SQLite has no date type; EF stores dates as text and
returns them with `DateTimeKind.Unspecified`, which silently breaks `dueDate < now` comparisons
on a machine that is not on UTC. Converters normalise to UTC on write and re-tag on read.

**`TimeProvider` injected instead of `DateTime.UtcNow`.** Built into .NET, no extra dependency,
and it makes every time-dependent rule testable without sleeping or rewriting dates.

**`ON DELETE SET NULL` in the schema, not just in code.** The "deleting a project must not
delete its tasks" rule is enforced by the database itself, so it holds even for direct SQL.

**One global validation filter.** Registering `ValidationFilter` once means no controller ever
calls a validator, and all three sources of validation failure (FluentValidation, model binding,
service-level rules) produce an identical error body.

**A strict enum JSON converter.** The built-in converter rejects unknown values with a message
that names the internal .NET type. The custom one lists the supported values instead, which is
what an API client actually needs.

**Migrations applied at startup.** Convenient for a project that is meant to be cloned and run.
In a production deployment this would normally move to a deliberate release step.

---

## Limitations

These are conscious scope boundaries rather than defects:

- **No authentication or authorisation.** The API is single-user and unprotected; it is intended
  to run locally.
- **SQLite does not enforce string lengths.** The `maxLength` values reach the migration, but
  SQLite's `TEXT` has no length constraint, so the 100/1000 limits are enforced by
  FluentValidation at the API boundary. Both layers read the same constants in
  `Models/ValidationLimits.cs`, so they cannot drift apart.
- **`PUT` is a full replacement, not a patch.** Omitting an optional field clears it. There is no
  `PATCH` endpoint.
- **`dueDate` is nullable**, on the basis that not every personal task has a deadline. A task
  without one is never counted as overdue.
- **Project names are not unique.** Two projects may share a name; they are distinguished by id.
- **No soft delete.** Deleting a task removes the row permanently.
- **Timestamps are UTC only.** No per-user time zone handling; clients are expected to convert
  for display.
- **Statistics are computed on request.** There is no caching, which is fine at personal data
  volumes but would need revisiting at scale.
