# GenericTestWebApi

.NET 8 Web API backend for the TestPrep online mock-test application.

## Database

The application uses **Neon PostgreSQL** with Entity Framework Core/Npgsql. The database schema and initial TestPrep data are created automatically on the first successful application startup by `EnsureCreatedAsync()` and the database initializer.

Do not commit the Neon connection string, passwords, JWT secrets, Google credentials, or Razorpay credentials to GitHub.

### Run locally with .NET user-secrets

From the API repository:

```bash
dotnet restore
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<YOUR_NEON_CONNECTION_STRING>"
dotnet run
```

Alternatively, set the `DATABASE_URL` environment variable before running the API.

PowerShell example:

```powershell
$env:DATABASE_URL = "<YOUR_NEON_CONNECTION_STRING>"
dotnet run
```

The Neon connection string must be a PostgreSQL/Npgsql connection string, for example:

```text
Host=<neon-host>;Database=neondb;Username=<neon-user>;Password=<neon-password>;SSL Mode=Require;Channel Binding=Require
```

On first startup the API creates the application tables, roles, development admin account, subscription plans, exam categories, mock tests, and seed questions.

### Development admin

The development seed creates:

- Email: `admin@testprep.local`
- Password: `Admin@123`

Change/remove development credentials before production deployment.

## Run locally

1. Install the .NET 8 SDK.
2. Configure the Neon connection using user-secrets or `DATABASE_URL` as described above.
3. From this repository run:

```bash
dotnet restore
dotnet run
```

4. Open Swagger using the HTTPS URL shown by ASP.NET when the application starts, commonly `https://localhost:7234/swagger`.
5. If the HTTPS development certificate is not trusted, run:

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

## Frontend

The React/Vite frontend should point `VITE_API_BASE_URL` at the local API URL, for example:

```text
https://localhost:7234/api
```

Use the actual HTTPS URL printed by `dotnet run` if the port differs.
