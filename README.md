# GenericTestWebApi

.NET 8 Web API backend for the TestPrep online mock-test application.

## Run locally

1. Install the .NET 8 SDK.
2. From this repository run:

```bash
dotnet restore
dotnet run
```

3. Open Swagger at `https://localhost:7234/swagger`.
4. If the HTTPS development certificate is not trusted, run:

```bash
dotnet dev-certs https --trust
```

## API endpoints

- `GET /api/health`
- `GET /api/exams/categories`
- `GET /api/exams/{category}/tests`
- `GET /api/tests/{testId}`
- `GET /api/tests/{testId}/questions`
- `POST /api/tests/{testId}/submit`

The current implementation intentionally uses in-memory seed data so the UI/API flow can be developed before introducing SQL Server and Entity Framework Core.
