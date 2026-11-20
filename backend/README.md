# DevTracker API: errors and Swagger UI

From the repository root, start PostgreSQL, configure the connection string, apply
migrations, then run the backend. This requires .NET SDK 10, Docker Desktop, and
the `dotnet-ef` tool version 10.0.13.

```powershell
docker compose up -d db
# Use the password configured for your local PostgreSQL instance.
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=devtracker;Username=postgres;Password=<local-password>'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project backend/src/DevTracker.Infrastructure --startup-project backend/src/DevTracker.Api
dotnet run --project backend/src/DevTracker.Api --launch-profile http
```

Alternatively, put `ConnectionStrings:DefaultConnection` in the ignored
`backend/src/DevTracker.Api/appsettings.Development.json`. Environment variables
override JSON configuration. Migrations are applied explicitly, not at app startup.

- Swagger UI: http://localhost:5033/swagger
- OpenAPI JSON: http://localhost:5033/openapi/v1.json

Both documentation endpoints are enabled only in `Development`. Swagger UI uses
the existing OpenAPI generator. Sign in through `POST /api/auth/login` in the UI;
the browser sends the Identity authentication cookie on subsequent requests.
Database operations require PostgreSQL and the configured connection string.

## Global exception handling

`Api/Exceptions/GlobalExceptionHandler.cs` implements `IExceptionHandler`.
`Program.cs` registers it with `AddExceptionHandler` and `AddProblemDetails`,
and enables it with `UseExceptionHandler`. Controllers let service exceptions
propagate to this handler instead of catching them individually.

| Exception | HTTP status | Title |
| --- | --- | --- |
| `ProjectDoNotExistsException` | 404 | Project not found |
| `TaskDoNotExistsException` | 404 | Task not found |
| `ProjectAlreadyExistsException` | 409 | Project already exists |
| `ProjectHasUnfinishedTasksException` | 409 | Cannot archive a project with unfinished tasks |
| `ProjectArchivedException` | 409 | Cannot create or update tasks in an archived project |
| `BadHttpRequestException` | Its HTTP status | Invalid request |
| Any other exception, including `FailedToUpdateProjectException` | 500 | An unexpected error occurred |

Responses use `application/problem+json`, with `status`, `title`, `instance`
(request path), and `traceId`. Unexpected server exceptions are logged with
the same trace ID. Exception messages and stack traces are not exposed.

To handle a new application exception, add a case to the handler's switch and
document its HTTP response on the affected controller actions using
`ProducesResponseType`. The handler does not automatically generate OpenAPI
response metadata.

Projects document the duplicate-name exception on POST/PATCH and the missing/inaccessible
project exception on GET-by-ID, PATCH, and DELETE. Tasks document the project
exception on all actions, plus the task exception on PATCH and DELETE. All
controllers document the fallback 500 response. Authentication failures use
401; DTO validation uses 400 and `ValidationProblemDetails`. Registration can
also return an array of Identity validation errors with 400.

## DTO validation, archiving, sessions and roles

Project names/task titles must contain non-whitespace text and have at most
150 characters. Descriptions have a 4000-character limit. Auth validates required
fields, email format/length and nickname length; Identity validates passwords.
Undefined enum values and malformed dates return 400 with field-level errors.
PATCH preserves omitted fields; explicit null clears description/dueAt.

`POST /api/projects/{projectId}/archive` returns 204 when the project has no
unfinished tasks, or 409 with `ProjectHasUnfinishedTasksException` when any task
is ToDo/InProgress. This rule lives in `ProjectService`. A successful archive
sets `ArchivedAt` in UTC; repeating it preserves the original timestamp.
Missing or inaccessible projects return 404.

Archiving and all project/task mutations acquire the same PostgreSQL project
row lock inside a Read Committed transaction. The lock remains held through
validation, SaveChanges and commit, and is released on rollback/cancellation.
Concurrent task creation/reopening either completes first and blocks archiving
with 409, or waits for the archive and is rejected with 409 afterwards.
POST/PATCH Tasks are rejected for archived projects; task deletion is allowed.

Login protection defaults to **5 failed passwords → 15-minute account lockout**,
plus **10 login requests per IP per 60-second fixed window**. Locked accounts
return the same 401 as invalid credentials. Successful sign-in resets the failure
counter. Request limits return 429 ProblemDetails with Retry-After.
Configure `Authentication:LoginProtection` using `MaxFailedAttempts`,
`LockoutMinutes`, `PermitLimit` and `WindowSeconds` (or environment variables
with `__` separators). These settings are validated on startup.

The IP limiter uses the connection peer address, not a client-supplied forwarding
header, and is process-local. Multiple API replicas need shared edge limiting
for a cluster-wide IP quota; account lockouts remain persisted in PostgreSQL.

`POST /api/auth/logout` requires a valid session, signs out with Identity and
deletes the current browser authentication cookie. Subsequent protected requests
need a new login. Logout uses normal cookie sign-out, without a server-side
session store or revoking a previously copied cookie on another client.

Sessions expire **30 minutes after sign-in**, without sliding renewal.
Configure `Authentication:Session:LifetimeMinutes` (1–1440), or the environment
variable `Authentication__Session__LifetimeMinutes`. The cookie is HttpOnly,
SameSite=Lax, and requires HTTPS outside Development. Expired/invalid sessions
return 401; missing roles return 403, without HTML login redirects.

The migrations create `USER` and `ADMIN` roles and give existing users the
baseline `USER` role. Registration creates the user and assigns `USER` in one
database transaction. Client-supplied role fields cannot grant ADMIN.
`GET /api/auth/me` includes the user's roles. `GET /api/admin/users` lists user
IDs, emails, nicknames and roles only for ADMIN.

To grant ADMIN, first register the account normally, then run this explicit
command locally with access to the server's database configuration:

```powershell
dotnet run --project backend/src/DevTracker.Api --launch-profile http -- --grant-admin existing-user@example.com
```

This command grants the role and exits; it does not start another HTTP server.
It does not create users or passwords, and is safe to repeat. Sign in again to
receive the new role in the authentication cookie. There is no public endpoint
for self-assigning ADMIN. ADMIN keeps its USER role; project ownership checks
still apply to both roles.

## Task pagination, filtering and ownership

```http
GET /api/projects/{projectId}/tasks?pageNumber=1&pageSize=20&status=0&search=login
```

The response is now an object instead of the previous bare array:

```json
{
  "items": [],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 0,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

Defaults are page 1 and 20 items. `pageSize` must be 1–100. Invalid or overflowing
pagination parameters, invalid statuses and search text longer than 150 characters
return 400 with validation errors. A valid page beyond the last returns empty
items and retains the filtered total count.

`status`: 0=ToDo, 1=InProgress, 2=Done. `search` matches a literal substring of
the title, case-insensitively; surrounding whitespace is trimmed. `%`, `_` and
backslash are treated literally. Empty/whitespace-only search means no filter.
Both filters can be combined with pagination; counting and paging happen in
PostgreSQL after filtering. Ordering is `CreatedAt`, then `Id`.

Ownership is checked before querying results. Missing or foreign projects return
404 and expose no items or counts. ADMIN can access the admin endpoint but does
not bypass project ownership. The frontend task view uses the new `items`
contract, filters and page navigation.

## Verification

```powershell
dotnet build backend/DevTracker.slnx
dotnet test backend/DevTracker.slnx
```

The exception middleware and documentation tests run without PostgreSQL. They
verify error mapping, safe response bodies, Swagger UI assets, OpenAPI error
descriptions, unauthorized access, and hiding documentation in Production.

To also run the PostgreSQL CRUD tests:

```powershell
$env:DEVTRACKER_TEST_CONNECTION = 'Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=<local-password>'
dotnet test backend/DevTracker.slnx
```

The database test account needs permission to create databases. Tests create a
database named `devtracker_tests_<random-id>`, apply all migrations, and delete
only that isolated database afterwards. Without the variable, database tests
are explicitly skipped. They never use the application's `devtracker` database.

The completed endpoint inventory, data model, PATCH semantics and verification
results for plan items 01–04 are in [docs/api-audit.md](docs/api-audit.md).

`ArchiveProjectTests` exercise the business rule without HTTP or a database.
`AuthAndArchiveIntegrationTests` verify validation, archiving, logout, roles,
and session expiration with a controlled `TimeProvider`, without real sleeps.

`ProjectConcurrencyTests` run parallel HTTP requests against PostgreSQL and
observe the blocked writer in `pg_stat_activity`. `LoginProtectionTests` cover
account lockout, reset/expiry, configuration, IP isolation, 429 and window renewal.
